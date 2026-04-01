using System;
using Vortex.Core.LoggerSystem.Bus;
using Vortex.Core.LoggerSystem.Model;
using Vortex.Core.SettingsSystem.Bus;
using Vortex.Core.System.Enums;

namespace Vortex.Core.AppSystem.Bus
{
    public static partial class App
    {
        /// <summary>
        /// Событие изменения состояния приложения
        /// </summary>
        public static event Action<AppStates> OnStateChanged;

        /// <summary>
        /// Событие начала запуска приложения
        /// </summary>
        public static event Action OnStarting;

        /// <summary>
        /// Системные контроллеры загружены, Приложение запущено
        /// </summary>
        public static event Action OnStart;

        /// <summary>
        /// Событие начала выхода из приложения
        /// </summary>
        public static event Action OnExit;

        /// <summary>
        /// Возвращает текущее состояние приложения
        /// </summary>
        /// <returns></returns>
        public static AppStates GetState() => Data._state;

        /// <summary>
        /// Выставить состояние приложения
        /// </summary>
        /// <param name="state"></param>
        /// <returns></returns>
        public static bool SetState(AppStates state)
        {
            if (_data._state == state)
                return false;

            if (Settings.Data() == null)
            {
                _data._state = AppStates.WaitSettings;
                return false;
            }

            if (Settings.Data().AppStateDebugMode)
                Log.Print(new LogData(LogLevel.Common, $"AppState: {state}", "App"));

            var old = _data._state;
            _data._state = state;
            OnStateChanged?.Invoke(state);
            if ((old == AppStates.Starting || old == AppStates.Unfocused) && _data._state == AppStates.Running)
                OnStart?.Invoke();

            switch (_data._state)
            {
                case AppStates.Starting:
                    OnStarting?.Invoke();
                    break;
                case AppStates.Stopping:
                    OnExit?.Invoke();
                    break;
            }

            return true;
        }
    }
}