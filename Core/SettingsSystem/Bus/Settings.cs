using Vortex.Core.SettingsSystem.Model;
using Vortex.Core.System.Abstractions;

namespace Vortex.Core.SettingsSystem.Bus
{
    /// <summary>
    /// Шина доступа к общим настройкам проекта
    /// </summary>
    public class Settings : SystemController<Settings, IDriver>
    {
        public static SettingsModel Data() => Driver?.GetData();

        protected override void OnDriverConnect()
        {
            //Ignore
        }

        protected override void OnDriverDisconnect()
        {
            //Ignore
        }
    }
}