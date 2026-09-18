using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Vortex.Core.DatabaseSystem.Bus;
using Vortex.Core.Extensions.ReactiveValues;
using Vortex.Sdk.RecordMarksSystem.Bus;
using Vortex.Sdk.RecordMarksSystem.Config;
using Vortex.Unity.DatabaseSystem.Attributes;
using Vortex.Unity.EditorTools.Attributes;
using Vortex.Unity.UI.PoolSystem;
using Vortex.Unity.UI.TweenerSystem;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Vortex.Sdk.GallerySystem.View
{
    /// <summary>
    /// Виджет-пул галлереи. Собирает записи-<see cref="IGalleryEntry"/> из <c>Database</c>,
    /// фильтрует по настройкам (marks + типы + guid + lockedMode), сортирует опциональным
    /// компаратором, наполняет <see cref="Pool"/>. Не участвует в разблокировке —
    /// статус читается из <see cref="RecordMarksBus"/> без записи.
    ///
    /// Focus и Show разведены: Focus обновляет реактив выбранного guid и статик-память
    /// «последней просмотренной». Show вызывает сама карточка — этот компонент только
    /// принимает уведомление через <see cref="GalleryPoolCallbacks.OnShow"/>.
    ///
    /// «Последняя просмотренная» живёт статически в рамках сессии — переживает
    /// переоткрытия сцены, не сохраняется в save-систему.
    /// </summary>
    public class GalleryView : MonoBehaviour
    {
        [Header("Data source")]
        [SerializeField, ValueSelector(nameof(GetAvailableMarks))]
        private string[] marks = new string[0];

        [SerializeField, ValueSelector(nameof(GetGalleryEntryTypes))]
        private string[] allowedTypes = new string[0];

        [SerializeField, ValueSelector(nameof(GetGalleryEntryTypes))]
        private string[] deniedTypes = new string[0];

        [SerializeField, DbRecord(GroupByType = true)]
        private string[] deniedGuids = new string[0];

        [Header("Behaviour")]
        [SerializeField] private LockedMode lockedMode = LockedMode.Hide;

        [SerializeField, ValueSelector(nameof(GetSorters))]
        private string sorter;

        [SerializeField, Tooltip("Сбрасывать выделение (highlight) в null, если последняя просмотренная " +
                                 "карточка отсутствует в текущем пуле. Выкл — выделение сохраняет прежнее значение.")]
        private bool resetSelectionWhenLastViewedAbsent = true;

        [Header("Overrides")]
        [SerializeField] private List<IconOverride> iconOverrides = new();

        [Header("Scene")]
        [SerializeField, AutoLink] private Pool pool;
        [SerializeField, AutoLink] private TweenerHub stubTweener;

        // Общая для всех инстансов сессионная память последней просмотренной карточки.
        // Пишется при HandleFocus / Open, читается при OnEnable для восстановления фокуса.
        private static string _lastViewedGuid;

        /// <summary>Read-only снаружи. Заполняется при фокусе на карточке или Open(guid).</summary>
        public static string LastViewedGuid => _lastViewedGuid;

        // Общий реактив выбранного guid на весь пул: одна подписка каждого PoolItem'а
        // достаточно для перерисовки highlight по всей галлерее.
        private StringData _selectedGuid;

        // Быстрый ответ на вопрос «есть ли guid в текущем пуле» (для Open и восстановления фокуса).
        private readonly HashSet<string> _currentPoolGuids = new();

        private void OnEnable()
        {
            if (pool == null)
            {
                Debug.LogError($"[GalleryView] '{name}': Pool reference is missing.");
                return;
            }

            // StringData создаётся один раз с owner=this — только этот компонент может её менять.
            _selectedGuid ??= new StringData(null, this);

            // Если Bus ещё не готов при первом OnEnable (гонка сцены и Bootstrap),
            // подписываемся на готовность и перезаполняем.
            if (!RecordMarksBus.IsReady)
            {
                RecordMarksBus.OnReady -= RefreshPool;
                RecordMarksBus.OnReady += RefreshPool;
                return;
            }

            RefreshPool();
        }

        private void OnDisable()
        {
            RecordMarksBus.OnReady -= RefreshPool;
            if (pool != null)
                pool.Clear();
            _currentPoolGuids.Clear();
        }

        /// <summary>
        /// Внешний позиционер: устанавливает фокус на карточке по guid, если она в пуле.
        /// При отсутствии — warning, состояние не меняется.
        /// </summary>
        public void Open(string guid)
        {
            if (string.IsNullOrEmpty(guid))
                return;
            if (!_currentPoolGuids.Contains(guid))
            {
                Debug.LogWarning($"[GalleryView] '{name}': Open('{guid}') — guid not in current pool.");
                return;
            }

            _selectedGuid.Set(guid, this);
            _lastViewedGuid = guid;
        }

        private void RefreshPool()
        {
            pool.Clear();
            _currentPoolGuids.Clear();

            var raw = Database.GetRecords(typeof(IGalleryEntry));

            var allowedSet = allowedTypes != null && allowedTypes.Length > 0
                ? new HashSet<string>(allowedTypes)
                : null;
            var deniedSet = deniedTypes != null && deniedTypes.Length > 0
                ? new HashSet<string>(deniedTypes)
                : null;
            var deniedGuidsSet = deniedGuids != null && deniedGuids.Length > 0
                ? new HashSet<string>(deniedGuids)
                : null;

            // (entry, unlocked): статус метки считается один раз здесь и переиспользуется
            // и фильтром lockedMode, и BoolData isLocked ниже — без повторного IsMarkedByAny.
            var filtered = new List<(IGalleryEntry entry, bool unlocked)>(raw.Length);
            for (var i = 0; i < raw.Length; i++)
            {
                var rec = raw[i];
                var typeName = rec.GetType().FullName;
                if (allowedSet != null && !allowedSet.Contains(typeName)) continue;
                if (deniedSet != null && deniedSet.Contains(typeName)) continue;

                var entry = (IGalleryEntry)rec;
                if (deniedGuidsSet != null && deniedGuidsSet.Contains(entry.GuidPreset)) continue;

                var unlocked = IsMarkedByAny(entry.GuidPreset);
                if (lockedMode == LockedMode.Hide && !unlocked) continue;

                filtered.Add((entry, unlocked));
            }

            var comparer = ResolveSorter();
            if (comparer != null)
                filtered.Sort((a, b) => comparer.Compare(a.entry, b.entry));

            if (filtered.Count == 0)
            {
                if (stubTweener != null) stubTweener.Forward();
                RestoreSelection();
                return;
            }

            if (stubTweener != null) stubTweener.Back();

            for (var i = 0; i < filtered.Count; i++)
            {
                var (entry, unlocked) = filtered[i];
                var preview = ResolveIcon(entry);
                var isLocked = new BoolData(!unlocked, this);

                // Замыкание захватывает конкретный entry — карточка получает индивидуальные колбэки
                // без необходимости передавать guid в сигнатуре.
                var captured = entry;
                var callbacks = new GalleryPoolCallbacks
                {
                    OnFocus = () => HandleFocus(captured.GuidPreset),
                    OnShow  = () => HandleShow(captured.GuidPreset)
                };

                pool.AddItem(new object[] { preview, isLocked, _selectedGuid, entry, callbacks });
                _currentPoolGuids.Add(entry.GuidPreset);
            }

            RestoreSelection();
        }

        /// <summary>
        /// Восстановить выделение по «последней просмотренной»: если она в текущем пуле — выделить её.
        /// Иначе, при включённом <see cref="resetSelectionWhenLastViewedAbsent"/> (по умолчанию) —
        /// сбросить выделение в <c>null</c>; при выключенном — оставить прежнее значение.
        /// </summary>
        private void RestoreSelection()
        {
            if (!string.IsNullOrEmpty(_lastViewedGuid) && _currentPoolGuids.Contains(_lastViewedGuid))
                _selectedGuid.Set(_lastViewedGuid, this);
            else if (resetSelectionWhenLastViewedAbsent)
                _selectedGuid.Set(null, this);
        }

        private void HandleFocus(string guid)
        {
            _selectedGuid.Set(guid, this);
            _lastViewedGuid = guid;
        }

        /// <summary>
        /// Пусто по контракту — <c>entry.Show()</c> вызывает сама карточка.
        /// Точка расширения для обрамляющего UI (счётчики, подсказки, аналитика) —
        /// достаточно унаследоваться или подписать наблюдателя.
        /// </summary>
        protected virtual void HandleShow(string guid)
        {
        }

        private bool IsMarkedByAny(string guid)
        {
            if (marks == null || marks.Length == 0) return false;
            for (var i = 0; i < marks.Length; i++)
            {
                if (RecordMarksBus.IsMarked(marks[i], guid))
                    return true;
            }
            return false;
        }

        private Sprite ResolveIcon(IGalleryEntry entry)
        {
            if (iconOverrides != null)
            {
                for (var i = 0; i < iconOverrides.Count; i++)
                {
                    var ov = iconOverrides[i];
                    if (ov != null && ov.Guid == entry.GuidPreset && ov.Sprite != null)
                        return ov.Sprite;
                }
            }
            return entry.Icon;
        }

        private IGalleryEntryComparer ResolveSorter()
        {
            if (string.IsNullOrEmpty(sorter)) return null;

            var type = Type.GetType(sorter);
            if (type == null)
            {
                // Type.GetType не находит сборки помимо текущей — сканируем домен.
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    type = asm.GetType(sorter);
                    if (type != null) break;
                }
            }

            if (type == null)
            {
                Debug.LogWarning($"[GalleryView] '{name}': sorter type '{sorter}' not found.");
                return null;
            }
            if (!typeof(IGalleryEntryComparer).IsAssignableFrom(type))
            {
                Debug.LogWarning($"[GalleryView] '{name}': sorter type '{sorter}' does not implement IGalleryEntryComparer.");
                return null;
            }

            try
            {
                return (IGalleryEntryComparer)Activator.CreateInstance(type);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[GalleryView] '{name}': failed to instantiate sorter '{sorter}': {ex.Message}");
                return null;
            }
        }

#if UNITY_EDITOR
        // ================= Editor-only провайдеры для [ValueSelector] =================

        // Метки — берём из RecordMarksSettings (Resources/Settings/RecordMarksSettings).
        // Читаем через Resources.Load, а не AssetDatabase, чтобы провайдер работал единообразно
        // и в редакторе, и при возможных playmode-инспекциях; сам Resources.Load в редакторе
        // корректно резолвит SO из Resources.
        private string[] GetAvailableMarks()
        {
            var settings = Resources.Load<RecordMarksSettings>("Settings/RecordMarksSettings");
            if (settings == null)
                return Array.Empty<string>();

            var slot = settings.SlotMarks ?? Array.Empty<string>();
            var global = settings.GlobalMarks ?? Array.Empty<string>();
            var result = new HashSet<string>();
            foreach (var m in slot)
                if (!string.IsNullOrWhiteSpace(m)) result.Add(m);
            foreach (var m in global)
                if (!string.IsNullOrWhiteSpace(m)) result.Add(m);
            var arr = result.ToArray();
            Array.Sort(arr, StringComparer.OrdinalIgnoreCase);
            return arr;
        }

        // Типы реализаций IGalleryEntry — key=короткое имя, value=FullName (пишется в поле).
        private Dictionary<string, string> GetGalleryEntryTypes()
            => ScanConcreteTypes<IGalleryEntry>();

        // Типы реализаций IGalleryEntryComparer. Первым пунктом выпадашки — явная
        // опция "None": выбор пишет "" в sorter, ResolveSorter трактует как «без сортировки».
        // Даёт дизайнеру нажать «сбросить» из UI, а не оставлять поле в состоянии «[NULL]».
        private Dictionary<string, string> GetSorters()
        {
            var result = new Dictionary<string, string> { { "None", "" } };
            foreach (var kv in ScanConcreteTypes<IGalleryEntryComparer>())
                if (!result.ContainsKey(kv.Key))
                    result[kv.Key] = kv.Value;
            return result;
        }

        private static Dictionary<string, string> ScanConcreteTypes<TBase>()
        {
            var result = new Dictionary<string, string>();
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try { types = asm.GetTypes(); }
                catch (ReflectionTypeLoadException ex) { types = ex.Types.Where(t => t != null).ToArray(); }
                foreach (var t in types)
                {
                    if (t == null) continue;
                    if (t.IsAbstract || t.IsInterface) continue;
                    if (!typeof(TBase).IsAssignableFrom(t)) continue;
                    if (t.FullName == null) continue;
                    if (!result.ContainsKey(t.Name))
                        result[t.Name] = t.FullName;
                    else
                        // Разрешаем неоднозначность: если два типа с одним именем в разных namespace,
                        // ключ уже занят — добавляем FullName как ключ, чтобы оба были видны.
                        result[t.FullName] = t.FullName;
                }
            }
            return result;
        }
#endif
    }
}
