using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using Vortex.Core.AppSystem.Bus;
using Vortex.Core.Extensions.LogicExtensions;
using Vortex.Core.System.ProcessInfo;
using Vortex.Core.LoggerSystem.Bus;
using Vortex.Core.LoggerSystem.Model;
using Vortex.Core.SettingsSystem.Bus;
using Vortex.Core.System.Abstractions;
using Vortex.Core.System.Enums;

namespace Vortex.Core.LoaderSystem.Bus
{
    /// <summary>
    /// Система загрузки приложения
    ///
    /// Загружаемые модули регистрируются через Register<T>() ДО начала загрузки
    /// Старт загрузки происходит по сигналу снаружи через Run() (зависит от платформы и реализации запуска)
    ///
    /// Порядок загрузки выстраивается автоматически по критерию "необходимой предзагрузки".
    /// То есть каждый ILoadable на запрос WaitingFor() должен вернуть перечень ILoadable,
    /// которые должны быть загружены вперед него
    /// </summary>
    public static class Loader
    {
        #region Events

        /// <summary>
        /// Событие начала загрузки
        /// </summary>
        public static event Action OnLoad;

        /// <summary>
        /// Событие завершения загрузки
        /// </summary>
        public static event Action OnComplete;

        #endregion

        #region Params

        /// <summary>
        /// Прогресс загрузки
        /// </summary>
        private static int _progress;

        /// <summary>
        /// Общее кол-во шагов загрузки
        /// </summary>
        private static int _size;

        /// <summary>
        /// Данные загрузки текущего загружаемого модуля
        /// </summary>
        private static ProcessData _currentProcessSystem;

        /// <summary>
        /// токен-ресурс прерывания
        /// </summary>
        private static CancellationTokenSource _cts = new();

        /// <summary>
        /// Токен прерывания
        /// </summary>
        private static CancellationToken Token => _cts.Token;

        /// <summary>
        /// Очередь загрузки
        /// </summary>
        private static readonly Dictionary<Type, IProcess> Queue = new();

        /// <summary>
        /// Индекс системных контроллеров
        /// </summary>
        private static readonly Dictionary<Type, PropertyInfo> SysControllers = new();

        /// <summary>
        /// Защита от мультивызова
        /// </summary>
        private static bool _isRunning = false;

        #endregion

        #region Public

        /// <summary>
        /// Регистрация в очереди на загрузку
        /// <param name="process"></param>
        /// </summary>
        public static void Register(IProcess process)
        {
            if (Settings.Data()?.DebugMode ?? false)
                Log.Print(LogLevel.Common, $"Регистрация системы: {process.GetType().Name}", "Loader");

            var processType = process.GetType();
            if (Queue.ContainsKey(processType))
            {
                Log.Print(LogLevel.Warning, $"Попытка повторной регистрации системы: {process.GetType().Name}",
                    "Loader");
                return;
            }

            Queue.AddNew(process.GetType(), process);
            _size = Queue.Count;
        }

        /// <summary>
        /// Снятие системы с регистрации в очередь на загрузку
        /// </summary>
        /// <typeparam name="T"></typeparam>
        public static void UnRegister<T>() where T : IProcess, new()
        {
            var process = typeof(T);
            UnRegister(process);
        }

        /// <summary>
        /// Снятие системы с регистрации в очередь на загрузку
        /// </summary>
        /// <param name="processType"></param>
        public static void UnRegister(Type processType)
        {
            if (Queue.ContainsKey(processType))
                Queue.Remove(processType);
        }

        /// <summary>
        /// Снятие системы с регистрации в очередь на загрузку
        /// </summary>
        /// <param name="process"></param>
        public static void UnRegister(IProcess process)
        {
            var processType = process.GetType();
            if (Queue.ContainsKey(processType) && Queue[processType] == process)
                Queue.Remove(processType);
        }

        /// <summary>
        /// Прогресс загрузки
        /// </summary>
        /// <returns></returns>
        public static int GetProgress() => _progress;

        /// <summary>
        /// Общее кол-во шагов загрузки
        /// </summary>
        public static int GetSize() => _size;

        /// <summary>
        /// Данные загрузки текущего загружаемого модуля
        /// </summary>
        public static ProcessData GetCurrentLoadingData() => _currentProcessSystem;

        /// <summary>
        /// Запуск процесса загрузки модулей
        /// </summary>
        public static async UniTask Run()
        {
            if (_isRunning)
                return;

            SysControllers.Clear();

            try
            {
                var assemblies = AppDomain.CurrentDomain.GetAssemblies();
                foreach (var assembly in assemblies)
                {
                    var types = assembly.GetTypes()
                        .Where(t => !t.IsAbstract && !t.IsInterface && typeof(ISystemController).IsAssignableFrom(t));
                    foreach (var type in types)
                        SysControllers.Add(type, null);
                }

                OnLoad?.Invoke();
                _isRunning = true;
                App.OnExit += Destroy;
                if (Settings.Data().DebugMode)
                {
                    var sb = new StringBuilder();
                    foreach (var entry in Queue)
                        sb.Append(entry.Key.Name + "\n");
                    Log.Print(new LogData(LogLevel.Common,
                        $"Loader running for {Queue.Count} systems\n<b>{sb}</b>",
                        "Loader"));
                }

                try
                {
                    await Loading(Token);
                }
                catch (Exception ex)
                {
                    Log.Print(new LogData(LogLevel.Error,
                        ex.Message + "\n" + ex.StackTrace,
                        "AppLoader"));
                }

                OnComplete?.Invoke();
                App.OnExit -= Destroy;
                App.SetState(AppStates.Running);
            }
            finally
            {
                SysControllers.Clear();
            }
        }

        /// <summary>
        /// Запуск процесса одиночной загрузки отдельного модуля
        /// </summary>
        public static async UniTask RunAlone(IProcess controller)
        {
            _cts.Cancel();
            _cts.Dispose();
            _cts = new CancellationTokenSource();

            _progress = 1;
            _size = 1;
            OnLoad?.Invoke();
            App.OnExit += Destroy;

            _currentProcessSystem = controller.GetProcessInfo() ?? new ProcessData
            {
                Name = "Loading system",
                Progress = 1,
                Size = 1
            };

            Log.Print(new LogData(LogLevel.Common,
                $"{controller.GetType().Name}: loading...",
                "AppLoader"));

            await controller.RunAsync(Token);

            Log.Print(new LogData(LogLevel.Common,
                "Loading complete",
                "AppLoader"));

            OnComplete?.Invoke();
            App.OnExit -= Destroy;
        }

        #endregion

        #region Private

        private static void Destroy()
        {
            App.OnExit -= Destroy;
            _cts.Cancel();
        }

        /// <summary>
        /// Запуск асинхронного процесса загрузки
        /// </summary>
        private static async UniTask Loading(CancellationToken token)
        {
            App.SetState(AppStates.Starting);
            //Ждем все подписки
            var queue = Queue.Values.ToList();
            var loaded = new HashSet<Type>();
            while (queue.Count > 0)
            {
                if (token.IsCancellationRequested)
                {
                    await UniTask.CompletedTask;
                    return;
                }

                IProcess controller = null;
                var count = queue.Count;
                var check = true;
                for (var i = 0; i < count; i++)
                {
                    check = true;
                    controller = queue[i];
                    var waitFor = controller.WaitingFor() ?? Array.Empty<Type>();
                    foreach (var type in waitFor)
                    {
                        if (!Queue.ContainsKey(type)
                            && !typeof(ISystemController).IsAssignableFrom(type))
                        {
                            Log.Print(new LogData(LogLevel.Error,
                                $"The expected controller {type} not found",
                                "AppLoader"));
                            check = false;
                            continue;
                        }

                        if (loaded.Contains(type) || CheckControllerState(type))
                            continue;

                        check = false;
                        break;
                    }

                    if (!check) continue;

                    queue.RemoveAt(i);
                    break;
                }

                if (!check)
                {
                    Log.Print(LogLevel.Error,
                        "Cyclic or incorrect dependency detected! Unable to resolve loading order",
                        "AppLoader");
                    App.Exit();
                    return;
                }

                switch (controller)
                {
                    case null when queue.Count > 0:
                    {
                        var sb = new StringBuilder(queue[0].GetType().Name);
                        for (var j = 1; j < queue.Count; j++)
                        {
                            var systemController = queue[j];
                            sb.Append($", {systemController.GetType().Name}");
                        }

                        Log.Print(new LogData(LogLevel.Error,
                            $"Loading critical error! Can not set order for next controllers: {sb}",
                            "AppLoader"));
                        return;
                    }
                    case null:
                        Log.Print(new LogData(LogLevel.Error,
                            "Unknown error. Can not set order for next controllers",
                            "AppLoader"));
                        return;
                }

                _currentProcessSystem = controller.GetProcessInfo() ?? new ProcessData
                {
                    Name = "Loading system",
                    Progress = 1,
                    Size = 1
                };
                _progress++;
                Log.Print(new LogData(LogLevel.Common,
                    $"{controller.GetType().Name}: loading...",
                    "AppLoader"));
                await controller.RunAsync(Token);
                loaded.Add(controller.GetType());
                Log.Print(new LogData(LogLevel.Common,
                    $"{controller.GetType().Name}: loaded",
                    "AppLoader"));
            }

            Log.Print(new LogData(LogLevel.Common,
                "Loading complete",
                "AppLoader"));
        }

        private static bool CheckControllerState(Type type)
        {
            if (!SysControllers.TryGetValue(type, out var property))
                return false;
            if (property == null)
            {
                property = type.GetProperty("IsInit",
                    BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
                SysControllers[type] = property;
            }

            if (property == null)
                return false;
            return (bool)property.GetValue(null, null);
        }

        #endregion
    }
}