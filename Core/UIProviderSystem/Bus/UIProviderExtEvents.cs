using System;

namespace Vortex.Core.UIProviderSystem.Bus
{
    /// <summary>
    /// Контроллер-шина для работы с интерфейсами
    /// </summary>
    public static partial class UIProvider
    {
        #region Events

        /// <summary>
        /// Событие открытия окна
        /// </summary>
        public static event Action OnOpen;

        /// <summary>
        /// Событие закрытия окна
        /// </summary>
        public static event Action OnClose;

        #endregion

        #region Internal

        internal static void CallOnOpen() => OnOpen?.Invoke();
        internal static void CallOnClose() => OnClose?.Invoke();

        #endregion
    }
}