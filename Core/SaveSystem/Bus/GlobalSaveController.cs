using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using Vortex.Core.AppSystem.Bus;
using Vortex.Core.Extensions.LogicExtensions.SerializationSystem;
using Vortex.Core.LoggerSystem.Bus;
using Vortex.Core.LoggerSystem.Model;
using Vortex.Core.SaveSystem.Abstraction;
using Vortex.Core.SaveSystem.Model;
using Vortex.Core.SettingsSystem.Bus;
using Vortex.Core.System.Abstractions;
using Vortex.Core.System.Enums;
using Vortex.Core.System.ProcessInfo;

namespace Vortex.Core.SaveSystem.Bus
{
    /// <summary>
    /// Шина глобального хранилища — межсессионные данные, которые переживают слоты сохранения, новые игры
    /// и перезапуски. Модули данных (<see cref="IGlobalData"/>) находятся рефлексией и живут всё приложение.
    ///
    /// Жизненный цикл:
    /// - до загрузки модули отдают значения по умолчанию;
    /// - на фазе Starting (процесс <c>Loader</c>, его регистрирует подключённый драйвер) контейнер читается,
    ///   сохранённые данные заливаются в существующие экземпляры модулей; затем открывается шлюз
    ///   <c>OnInit</c> (<see cref="SystemController{T,TD}.IsInit"/> = хранилище прочитано);
    /// - изменения фиксирует модуль (<see cref="Commit{T}"/>): фиксации кадра схлопываются в одну запись в конце
    ///   кадра; в состояниях Unfocused и Stopping запись немедленная — после них кадров может не быть,
    ///   а порядок обработчиков Stopping не определён;
    /// - при Stopping — запись незаписанного и, если настроено, резервная копия (одна за запуск).
    ///
    /// Слоты сохранения (<see cref="SaveController"/>) глобальных данных не касаются.
    /// </summary>
    public partial class GlobalSaveController : SystemController<GlobalSaveController, IGlobalSaveDriver>, IProcess
    {
        private const string LogTag = "GlobalSaveController";

        private const BindingFlags StateFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

        private const string TypeMarker = "\"__\"";

        private static readonly ProcessData ProcessInfo = new("GlobalSave");

        private static GlobalModel _model;

        /// <summary>Модули по ключу. Модули с дублем ключа или без ключа сюда не входят — не читаются и не пишутся.</summary>
        private static readonly Dictionary<string, IGlobalData> ByKey = new();

        /// <summary>Ключи, объявленные несколькими модулями.</summary>
        private static readonly HashSet<string> Duplicates = new();

        /// <summary>Модули, чьи данные нашлись в прочитанном контейнере.</summary>
        private static readonly HashSet<Type> Stored = new();

        /// <summary>Модули, зафиксированные после последней записи.</summary>
        private static readonly HashSet<Type> Pending = new();

        /// <summary>При загрузке случилась ошибка, для которой DebugSettings требует fail-fast.</summary>
        private static bool _failFastHit;

        /// <summary>
        /// Записаны изменения модулей: одно уведомление за кадр (при Unfocused и Stopping — сразу), аргумент —
        /// типы изменённых модулей. Приходит независимо от успеха записи: данные в памяти уже изменены.
        /// </summary>
        public static event Action<IReadOnlyList<Type>> OnChanged;

        /// <summary>Модули, которые хранилище читает и пишет, по ключу. Для инструментов.</summary>
        public static IReadOnlyDictionary<string, IGlobalData> Modules => ByKey;

        private static GlobalModel Model
        {
            get
            {
                if (_model != null)
                    return _model;
                _model = new GlobalModel();
                _model.Init();
                return _model;
            }
        }

        #region Public API

        /// <summary>Модуль с текущими значениями. До загрузки — значения по умолчанию.</summary>
        public static T Get<T>() where T : class, IGlobalData => Model.Get<T>();

        /// <summary>
        /// Данные модуля нашлись в прочитанном контейнере. Нужен модулям для миграций: <c>false</c> — модуль
        /// ещё ни разу не был записан (или его данные не прочитались).
        /// </summary>
        public static bool HasStoredData<T>() where T : class, IGlobalData => Stored.Contains(typeof(T));

        /// <summary>
        /// Зафиксировать изменения модуля. Запись — в конце кадра, одна на все фиксации кадра; в состояниях
        /// Unfocused и Stopping — сразу. До загрузки хранилища фиксация отклоняется с ошибкой в лог.
        /// </summary>
        public static void Commit<T>() where T : class, IGlobalData
        {
            var type = typeof(T);
            if (!CanChange(type, "Фиксация"))
                return;

            Pending.Add(type);
            if (IsImmediate())
            {
                Flush();
                return;
            }

            Driver.ScheduleFlush(Flush);
        }

        /// <summary>Сбросить модуль к значениям по умолчанию в том же экземпляре, сразу записать, уведомить.</summary>
        public static void Reset<T>() where T : class, IGlobalData => Reset(typeof(T));

        /// <inheritdoc cref="Reset{T}"/>
        public static void Reset(Type moduleType)
        {
            if (!CanChange(moduleType, "Сброс"))
                return;

            ResetInPlace(Model.Find(moduleType));
            Pending.Add(moduleType);
            Flush();
        }

        /// <summary>Сбросить все модули к значениям по умолчанию в тех же экземплярах, сразу записать, уведомить.</summary>
        public static void ResetAll()
        {
            if (!IsInit)
            {
                Log.Print(LogLevel.Error, "Сброс всех модулей: глобальное хранилище не загружено — отклонено", LogTag);
                return;
            }

            foreach (var module in Model.Modules)
            {
                ResetInPlace(module);
                Pending.Add(module.GetType());
            }

            Flush();
        }

        #endregion

        #region Loading

        public ProcessData GetProcessInfo() => ProcessInfo;

        public Type[] WaitingFor() => null;

        public UniTask RunAsync(CancellationToken cancellationToken)
        {
            IsInit = false;
            _failFastHit = false;
            _copyDone = false;
            Pending.Clear();
            Stored.Clear();

            BuildIndex();

            // Чистый старт и при рестарте без выгрузки домена: экземпляры те же, значения — по умолчанию
            foreach (var module in Model.Modules)
                ResetInPlace(module);

            if (!TryReadStorage(out var folders, out var restored))
            {
                ReportError("Контейнер глобального хранилища нечитаем, целой резервной копии нет. " +
                            "Модули — со значениями по умолчанию; первая запись перезапишет контейнер.");
                folders = new Dictionary<string, string>();
            }

            Apply(folders);

            if (_failFastHit)
            {
                Log.Print(LogLevel.Error,
                    "Загрузка остановлена: fail-fast для ошибок глобального сохранения (DebugSettings). " +
                    "Запись заблокирована, хранилище не тронуто.", LogTag);
                App.Exit();
                return UniTask.CompletedTask;
            }

            // Испорченный основной контейнер заменяется поднятой копией сразу, не дожидаясь фиксаций
            if (restored)
                WriteMain();

            App.OnStateChanged -= OnAppStateChanged;
            App.OnStateChanged += OnAppStateChanged;

            CallOnInit();
            return UniTask.CompletedTask;
        }

        /// <summary>
        /// Индекс по ключам. Дубли не выбирают «первого»: порядок рефлексии между сборками не гарантирован,
        /// и данные одного модуля могли бы прочитаться в другой. Все участники совпадения остаются
        /// со значениями по умолчанию, не читаются и не пишутся.
        /// </summary>
        private static void BuildIndex()
        {
            ByKey.Clear();
            Duplicates.Clear();

            foreach (var module in Model.Modules)
            {
                var type = module.GetType();
                var key = ReadKey(module);
                if (string.IsNullOrEmpty(key))
                {
                    ReportError($"Модуль {type.Name} не объявил ключ — не читается и не записывается.");
                    continue;
                }

                if (Duplicates.Contains(key))
                {
                    ReportError($"Ключ «{key}» повторно объявлен модулем {type.Name} — не читается и не записывается.");
                    continue;
                }

                if (ByKey.TryGetValue(key, out var first))
                {
                    ByKey.Remove(key);
                    Duplicates.Add(key);
                    ReportError($"Ключ «{key}» объявлен модулями {first.GetType().Name} и {type.Name} — " +
                                "оба остаются со значениями по умолчанию, не читаются и не записываются.");
                    continue;
                }

                ByKey.Add(key, module);
            }
        }

        private static string ReadKey(IGlobalData module)
        {
            try
            {
                return module.GetGlobalKey();
            }
            catch (Exception e)
            {
                Log.Print(LogLevel.Error, $"{module.GetType().Name}.GetGlobalKey: {e.Message}", LogTag);
                return null;
            }
        }

        /// <summary>
        /// Прочитанные папки — в модули. Папка без модуля (пакет отключён) не читается и при следующей записи
        /// не сохраняется: отключение пакета считается окончательным.
        /// </summary>
        private static void Apply(Dictionary<string, string> folders)
        {
            foreach (var pair in folders)
            {
                if (ByKey.TryGetValue(pair.Key, out var module))
                {
                    if (TryUpload(module, pair.Value))
                        Stored.Add(module.GetType());
                    else
                        ReportError($"Данные модуля «{pair.Key}» ({module.GetType().Name}) нечитаемы — " +
                                    "модуль со значениями по умолчанию, при следующей записи данные будут заменены.");
                    continue;
                }

                if (Duplicates.Contains(pair.Key))
                    continue;

                Log.Print(LogLevel.Warning,
                    $"В контейнере есть данные «{pair.Key}» без модуля — не читаются и при следующей записи не сохраняются.",
                    LogTag);
            }
        }

        /// <summary>
        /// Залить данные в существующий экземпляр модуля. Сначала пробное чтение в новый экземпляр: заливка
        /// сериализатора статуса не возвращает, а нечитаемые данные не должны задеть модуль.
        /// </summary>
        private static bool TryUpload(IGlobalData module, string json)
        {
            var type = module.GetType();
            json = RetargetTypeMarker(json, type);
            if (json == null)
                return false;

            var probe = json.DeserializeProperties<IGlobalData>();
            if (probe == null || probe.GetType() != type)
                return false;

            json.UploadProperties(module);
            return true;
        }

        /// <summary>
        /// Сериализатор находит тип по корневому маркеру <c>"__"</c> (полное имя класса). Модуль адресуется
        /// ключом, поэтому маркер заменяется текущим типом модуля: переименование и перенос класса модуля
        /// данные не ломают. Имена вложенных типов — забота модуля.
        /// </summary>
        private static string RetargetTypeMarker(string json, Type type)
        {
            if (string.IsNullOrEmpty(json))
                return null;

            var marker = json.IndexOf(TypeMarker, StringComparison.Ordinal);
            if (marker < 0)
                return null;

            var open = json.IndexOf('"', marker + TypeMarker.Length);
            if (open < 0)
                return null;

            var close = json.IndexOf('"', open + 1);
            if (close < 0)
                return null;

            return json.Substring(0, open + 1) + type.AssemblyQualifiedName + json.Substring(close);
        }

        #endregion

        #region Changes

        private static bool CanChange(Type type, string operation)
        {
            if (!IsInit)
            {
                Log.Print(LogLevel.Error,
                    $"{operation} {type?.Name}: глобальное хранилище не загружено — операция отклонена", LogTag);
                return false;
            }

            if (Model.Find(type) != null)
                return true;

            Log.Print(LogLevel.Error, $"{operation}: модуль {type?.Name} не найден в глобальном хранилище", LogTag);
            return false;
        }

        private static bool IsImmediate() => App.GetState() is AppStates.Unfocused or AppStates.Stopping;

        private static void Flush()
        {
            if (Pending.Count == 0 || !IsInit)
                return;

            var changed = new List<Type>(Pending);
            Pending.Clear();
            WriteMain();
            OnChanged?.Invoke(changed);
        }

        /// <summary>
        /// Значения по умолчанию в том же экземпляре: свойства копируются из нового экземпляра, ссылки
        /// потребителей на модуль остаются рабочими. Коллекции заменяются коллекциями нового экземпляра,
        /// а не дописываются. Состояние модуля — его свойства: поля не сбрасываются.
        /// </summary>
        private static void ResetInPlace(IGlobalData module)
        {
            if (module == null)
                return;

            var type = module.GetType();
            object fresh;
            try
            {
                fresh = Activator.CreateInstance(type);
            }
            catch (Exception e)
            {
                Log.Print(LogLevel.Error, $"Сброс {type.Name}: {e.Message}", LogTag);
                return;
            }

            foreach (var prop in type.GetProperties(StateFlags))
            {
                if (!prop.CanRead || prop.SetMethod == null || prop.GetIndexParameters().Length > 0)
                    continue;
                prop.SetValue(module, prop.GetValue(fresh));
            }
        }

        private static void OnAppStateChanged(AppStates state)
        {
            switch (state)
            {
                case AppStates.Unfocused:
                    Flush();
                    break;
                case AppStates.Stopping:
                    Flush();
                    MakeCopy();
                    break;
            }
        }

        #endregion

        /// <summary>
        /// Ошибка чтения или конфигурации. В билде — только лог, загрузка продолжается. В редакторе при
        /// включённом тумблере DebugSettings загрузка по её итогам останавливается.
        /// </summary>
        private static void ReportError(string message)
        {
            Log.Print(LogLevel.Error, message, LogTag);
            if (Settings.Data()?.GlobalSaveFailFast ?? false)
                _failFastHit = true;
        }

        protected override void OnDriverConnect()
        {
        }

        protected override void OnDriverDisconnect()
        {
        }
    }
}
