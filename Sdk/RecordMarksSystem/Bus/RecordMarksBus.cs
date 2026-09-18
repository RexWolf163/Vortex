using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Vortex.Core.LoggerSystem.Bus;
using Vortex.Core.LoggerSystem.Model;
using Vortex.Core.SaveSystem.Bus;
using Vortex.Sdk.Core.GameCore;
using Vortex.Sdk.RecordMarksSystem.Config;
using Vortex.Sdk.RecordMarksSystem.Models;
using Vortex.Unity.AppSystem.System.TimeSystem;

namespace Vortex.Sdk.RecordMarksSystem.Bus
{
    /// <summary>
    /// Статическая шина пакета <c>RecordMarksSystem</c>. Единственная публичная точка API.
    ///
    /// Bootstrap идёт через <see cref="RuntimeInitializeOnLoadMethodAttribute"/> —
    /// пакет не входит в общий <c>Loader</c>/<c>DriverConfig</c>, инициализируется
    /// самостоятельно при старте приложения.
    ///
    /// Двухфазная готовность:
    /// - Bootstrap читает <see cref="RecordMarksSettings"/> из <c>Resources/Settings/</c>,
    ///   валидирует и подписывается на события двух шин: <c>GlobalSaveController.OnInit</c>
    ///   (одноразовое, для per-account модели) и <c>GameController.OnNewGame/OnLoadGame</c>
    ///   (многократное, слот-модель пересоздаётся на каждой новой сессии).
    /// - <see cref="IsReady"/> = <c>true</c> только когда обе модели резолвлены. До этого
    ///   любой <c>Mark</c>/<c>Unmark</c> — log-warning + no-op (Open Q5 = A).
    ///
    /// События <see cref="OnMarked"/> и <see cref="OnUnmarked"/> батчатся через
    /// <see cref="TimeController.Accumulate{T}"/> — фаерятся один раз за <c>LateUpdate</c>,
    /// пробегая по накопленной очереди. Дополнительно поднимается стандартный
    /// <c>GameController.CallUpdateEvent</c> — дефолтный реактивный пайплайн Vortex.
    /// </summary>
    public static class RecordMarksBus
    {
        private const string SettingsResourcePath = "Settings/RecordMarksSettings";

        // Готовность
        public static bool IsReady { get; private set; }

        public static event Action OnReady;
        public static event Action OnMarksLoaded;
        public static event Action<string /*mark*/, string /*guid*/> OnMarked;
        public static event Action<string /*mark*/, string /*guid*/> OnUnmarked;

        // Конфиг
        private static RecordMarksSettings _settings;
        private static readonly HashSet<string> _validSlotMarks = new();
        private static readonly HashSet<string> _validGlobalMarks = new();

        // Модели + runtime-кеши для O(1) Contains/Add/Remove
        private static RecordMarksSlotData _slotModel;
        private static RecordMarksGlobalData _globalModel;
        private static readonly Dictionary<string, HashSet<string>> _slotCache = new();
        private static readonly Dictionary<string, HashSet<string>> _globalCache = new();

        // Очередь батчинга событий
        private static readonly Queue<PendingEvent> _pending = new();

        // ================= Public API =================

        /// <summary>
        /// Пометить GUID пресета указанной меткой. Ratchet-семантика: повторный вызов на уже
        /// помеченном GUID — тихий no-op, событие не фаерится.
        /// </summary>
        public static void Mark(string mark, string guid)
        {
            if (!EnsureReady()) return;

            var (data, cache, isGlobal) = Resolve(mark);
            if (data == null) return;
            if (string.IsNullOrEmpty(guid))
            {
                Log.Print(LogLevel.Warning, "[RecordMarks] Mark called with empty guid.", "RecordMarksBus");
                return;
            }

            if (!cache[mark].Add(guid)) return;
            data[mark].Add(guid);

            _pending.Enqueue(new PendingEvent(mark, guid, true));
            TimeController.Accumulate(FlushEvents, typeof(RecordMarksBus));

            if (isGlobal)
                GlobalSaveController.Commit<RecordMarksGlobalData>();
        }

        /// <summary>
        /// Снять метку с GUID. Публичный метод, но по контракту — для debug/cheat-меню
        /// разработчика. Штатный flow — только <see cref="Mark"/> (ratchet). No-op, если
        /// метки не было. Событие <see cref="OnUnmarked"/> фаерится только при реальном снятии.
        /// </summary>
        public static void Unmark(string mark, string guid)
        {
            if (!EnsureReady()) return;

            var (data, cache, isGlobal) = Resolve(mark);
            if (data == null) return;
            if (string.IsNullOrEmpty(guid)) return;

            if (!cache[mark].Remove(guid)) return;
            data[mark].Remove(guid);

            _pending.Enqueue(new PendingEvent(mark, guid, false));
            TimeController.Accumulate(FlushEvents, typeof(RecordMarksBus));

            if (isGlobal)
                GlobalSaveController.Commit<RecordMarksGlobalData>();
        }

        /// <summary>Проверка наличия метки на GUID. Для неизвестной метки — <c>false</c>.</summary>
        public static bool IsMarked(string mark, string guid)
        {
            if (!IsReady) return false;
            var (_, cache, _) = Resolve(mark);
            return cache != null && cache.TryGetValue(mark, out var set) && set.Contains(guid);
        }

        /// <summary>
        /// Все GUID, помеченные указанной меткой. Для неизвестной метки — пустой массив.
        /// Возвращается live-обёртка над внутренним <see cref="HashSet{T}"/> через
        /// <see cref="IReadOnlyCollection{T}"/> — без аллокации. Мутация через downcast
        /// нарушает контракт read-only; штатное чтение (Count/Contains/foreach) безопасно.
        /// </summary>
        public static IReadOnlyCollection<string> GetMarked(string mark)
        {
            if (!IsReady) return Array.Empty<string>();
            var (_, cache, _) = Resolve(mark);
            if (cache == null || !cache.TryGetValue(mark, out var set)) return Array.Empty<string>();
            return set;
        }

        // ================= Bootstrap =================

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            IsReady = false;
            _slotModel = null;
            _globalModel = null;
            _slotCache.Clear();
            _globalCache.Clear();
            _validSlotMarks.Clear();
            _validGlobalMarks.Clear();
            _pending.Clear();

            _settings = Resources.Load<RecordMarksSettings>(SettingsResourcePath);
            if (_settings == null)
            {
                Log.Print(LogLevel.Error,
                    $"[RecordMarks] Settings asset not found at Resources/{SettingsResourcePath}. Package disabled.",
                    "RecordMarksBus");
                return;
            }

            ValidateSettings();

            // -= then += защищает от накопления подписок в Fast Enter Play, когда статик
            // сбрасывается [RuntimeInitializeOnLoadMethod]'ом, а события живут на стороне
            // GameController/GlobalSaveController и удерживают старые ссылки на статик-делегаты.
            GlobalSaveController.OnInit -= OnGlobalReady;
            GlobalSaveController.OnInit += OnGlobalReady;
            GameController.OnNewGame -= OnSlotChanged;
            GameController.OnNewGame += OnSlotChanged;
            GameController.OnLoadGame -= OnSlotChanged;
            GameController.OnLoadGame += OnSlotChanged;
        }

        /// <summary>
        /// Inv-1: собираем множества валидных меток. Пустые имена, дубликаты в пределах одного
        /// списка и пересечения между списками исключаются из индекса (Open Q2 = B: warning + continue).
        /// </summary>
        private static void ValidateSettings()
        {
            AddValid(_settings.SlotMarks, _validSlotMarks, "slotMarks");
            AddValid(_settings.GlobalMarks, _validGlobalMarks, "globalMarks");

            foreach (var overlap in new List<string>(_validSlotMarks))
            {
                if (!_validGlobalMarks.Contains(overlap)) continue;
                Log.Print(LogLevel.Error,
                    $"[RecordMarks] Mark '{overlap}' declared in both slot and global lists. Excluded from index.",
                    "RecordMarksBus");
                _validSlotMarks.Remove(overlap);
                _validGlobalMarks.Remove(overlap);
            }
        }

        private static void AddValid(string[] source, HashSet<string> target, string listName)
        {
            if (source == null) return;
            foreach (var name in source)
            {
                if (string.IsNullOrWhiteSpace(name))
                {
                    Log.Print(LogLevel.Warning,
                        $"[RecordMarks] Empty mark name in '{listName}'. Skipped.", "RecordMarksBus");
                    continue;
                }

                if (!target.Add(name))
                    Log.Print(LogLevel.Warning,
                        $"[RecordMarks] Duplicate mark '{name}' in '{listName}'. Second occurrence skipped.",
                        "RecordMarksBus");
            }
        }

        private static void OnGlobalReady()
        {
            _globalModel = GlobalSaveController.Get<RecordMarksGlobalData>();
            if (_globalModel == null)
            {
                Log.Print(LogLevel.Error,
                    "[RecordMarks] RecordMarksGlobalData not registered in GlobalSaveController.",
                    "RecordMarksBus");
                return;
            }

            SyncModelWithSettings(_globalModel.Data, _validGlobalMarks);
            RebuildCache(_globalCache, _globalModel.Data);
            TryFinalizeReady();
        }

        private static void OnSlotChanged()
        {
            // Смягчение: событие в _pending от предыдущей сессии не должно фаериться после
            // смены слота — семантически принадлежит удалённой модели. Consumer перерисуется
            // штатно через OnMarksLoaded.
            _pending.Clear();

            _slotModel = GameController.Get<RecordMarksSlotData>();
            if (_slotModel == null)
            {
                Log.Print(LogLevel.Error,
                    "[RecordMarks] RecordMarksSlotData not registered in GameModel.",
                    "RecordMarksBus");
                return;
            }

            SyncModelWithSettings(_slotModel.Data, _validSlotMarks);
            RebuildCache(_slotCache, _slotModel.Data);

            TryFinalizeReady();
            OnMarksLoaded?.Invoke();
        }

        /// <summary>
        /// Гарантирует, что в <paramref name="modelData"/> присутствует ключ для каждой
        /// валидной метки (создаёт пустой список), и удаляет метки, не входящие в SO
        /// (Inv-6: неизвестные метки при загрузке — warning + drop).
        /// </summary>
        private static void SyncModelWithSettings(Dictionary<string, List<string>> modelData,
            HashSet<string> validMarks)
        {
            var toRemove = new List<string>();
            foreach (var key in modelData.Keys)
                if (!validMarks.Contains(key))
                    toRemove.Add(key);
            foreach (var key in toRemove)
            {
                Log.Print(LogLevel.Warning,
                    $"[RecordMarks] Mark '{key}' from save is not declared in Settings. Dropped.",
                    "RecordMarksBus");
                modelData.Remove(key);
            }

            foreach (var mark in validMarks)
                if (!modelData.ContainsKey(mark))
                    modelData[mark] = new List<string>();
        }

        private static void RebuildCache(Dictionary<string, HashSet<string>> cache,
            Dictionary<string, List<string>> source)
        {
            cache.Clear();
            foreach (var pair in source)
                cache[pair.Key] = new HashSet<string>(pair.Value);
        }

        private static void TryFinalizeReady()
        {
            if (IsReady) return;
            if (_slotModel == null || _globalModel == null) return;
            IsReady = true;
            OnReady?.Invoke();
        }

        // ================= Внутренняя маршрутизация =================

        private static (Dictionary<string, List<string>> data,
            Dictionary<string, HashSet<string>> cache,
            bool isGlobal) Resolve(string mark)
        {
            if (string.IsNullOrEmpty(mark))
            {
                Log.Print(LogLevel.Warning, "[RecordMarks] Empty mark name.", "RecordMarksBus");
                return (null, null, false);
            }

            if (_validSlotMarks.Contains(mark))
                return (_slotModel?.Data, _slotCache, false);
            if (_validGlobalMarks.Contains(mark))
                return (_globalModel?.Data, _globalCache, true);

            Log.Print(LogLevel.Warning,
                $"[RecordMarks] Unknown mark '{mark}' (not declared in RecordMarksSettings).",
                "RecordMarksBus");
            return (null, null, false);
        }

        private static bool EnsureReady()
        {
            if (IsReady) return true;
            Log.Print(LogLevel.Warning,
                "[RecordMarks] Bus is not ready yet. Subscribe to OnReady before mutating.",
                "RecordMarksBus");
            return false;
        }

        private static void FlushEvents()
        {
            while (_pending.Count > 0)
            {
                var e = _pending.Dequeue();
                if (e.IsMarked) OnMarked?.Invoke(e.Mark, e.Guid);
                else OnUnmarked?.Invoke(e.Mark, e.Guid);
            }

            GameController.CallUpdateEvent();
        }

        private readonly struct PendingEvent
        {
            public readonly string Mark;
            public readonly string Guid;
            public readonly bool IsMarked;

            public PendingEvent(string mark, string guid, bool isMarked)
            {
                Mark = mark;
                Guid = guid;
                IsMarked = isMarked;
            }
        }

#if UNITY_EDITOR
        // Editor-only accessor'ы для окна индекса. Не рассчитаны на runtime.
        internal static RecordMarksSettings EditorSettings => _settings;
        internal static IReadOnlyDictionary<string, HashSet<string>> EditorSlotCache => _slotCache;
        internal static IReadOnlyDictionary<string, HashSet<string>> EditorGlobalCache => _globalCache;
#endif
    }
}