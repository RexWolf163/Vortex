using System.Linq;
using Vortex.Core.LoggerSystem.Bus;
using Vortex.Core.LoggerSystem.Model;
using Vortex.Core.UIProviderSystem.Enums;
using Vortex.Core.UIProviderSystem.Model;

namespace Vortex.Core.UIProviderSystem.Bus
{
    /// <summary>
    /// Контроллер-шина для работы с интерфейсами
    /// </summary>
    public static partial class UIProvider
    {
        #region Public

        public static void Open(string uiId)
        {
            if (!Uis.TryGetValue(uiId, out var ui))
            {
                Log.Print(LogLevel.Error, $"[UIProvider] UI doesn't exist: {uiId}", "UIProvider");
                return;
            }

            ui.Open();
        }

        public static void Close(string uiId)
        {
            if (!Uis.TryGetValue(uiId, out var ui))
            {
                Log.Print(LogLevel.Error, $"[UIProvider] UI doesn't exist: {uiId}", "UIProvider");
                return;
            }

            ui.Close();
        }

        /// <summary>
        /// Закрыть все базовые интерфейсы (не относится к вторичным типа панелей, оверлеев или попапов)
        /// </summary>
        public static void CloseAll()
        {
            foreach (var ui in Uis)
            {
                if (ui.Value.UIType != UserInterfaceTypes.Common)
                    continue;
                ui.Value.Close();
            }
        }

        /// <summary>
        /// Проверка наличия открытых Common интерфейсов
        /// </summary>
        /// <returns></returns>
        public static bool HasOpenedUIs()
        {
            var list = Uis.Values;
            foreach (var ui in list)
            {
                if (ui.UIType != UserInterfaceTypes.Common)
                    continue;
                if (ui.IsOpen)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Возвращает открытые Common интерфейсы
        /// </summary>
        /// <returns></returns>
        public static UserInterfaceData[] GetOpenedUIs()
        {
            var list = Uis.Values;
            return list.Where(ui => ui.UIType == UserInterfaceTypes.Common).Where(ui => ui.IsOpen).ToArray();
        }

        #endregion
    }
}