using System.Collections.Generic;
using Vortex.Core.AppSystem.Bus;
using Vortex.Core.DatabaseSystem.Bus;
using Vortex.Core.Extensions.LogicExtensions;
using Vortex.Core.LoggerSystem.Bus;
using Vortex.Core.LoggerSystem.Model;
using Vortex.Core.SettingsSystem.Bus;
using Vortex.Core.System.Enums;
using Vortex.Core.UIProviderSystem.Model;

namespace Vortex.Core.UIProviderSystem.Bus
{
    /// <summary>
    /// Контроллер-шина для работы с интерфейсами
    /// Функционал регистрации
    /// </summary>
    public static partial class UIProvider
    {
        #region Params

        /// <summary>
        /// Индекс зарегистрировавшихся UI по Id их пресета
        /// </summary>
        private static readonly Dictionary<string, UserInterfaceData> Uis = new();

        #endregion

        #region Public

        /// <summary>
        /// Регистрация нового интерфейса в индексе
        /// </summary>
        /// <param name="id"></param>
        public static UserInterfaceData Register(string id)
        {
            if (App.GetState() < AppStates.Starting)
                return null;
            var ui = Database.GetRecord<UserInterfaceData>(id);
            if (Settings.Data().AppStateDebugMode)
                Log.Print(LogLevel.Common, $"[UIProvider] Registering UI : {ui.Name}", "UIProvider");
            Uis.AddNew(id, ui);
            return ui;
        }

        /// <summary>
        /// Снятие с регистрации интерфейса
        /// </summary>
        /// <param name="id"></param>
        public static void Unregister(string id)
        {
            if (!Uis.ContainsKey(id))
            {
                Log.Print(LogLevel.Error, $"[UIProvider] UI doesn't exist: {id}", "UIProvider");
                return;
            }

            var ui = Uis.Get(id);
            if (Settings.Data().AppStateDebugMode)
                Log.Print(LogLevel.Common, $"[UIProvider] UnRegistering UI : {ui.Name}", "UIProvider");
            Uis.Remove(id);
        }

        #endregion
    }
}