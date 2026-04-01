using Vortex.Core.SettingsSystem.Model;
using Vortex.Core.System.Abstractions;

namespace Vortex.Core.SettingsSystem
{
    public interface IDriver : ISystemDriver
    {
        public SettingsModel GetData();
    }
}