#if USING_VORTEX_MAP_LEVELS
namespace Vortex.Core.SettingsSystem.Model
{
    public class MapLevelsConfig
    {
        public MapLevelsConfig(string controller, int unloadDistance)
        {
            UnloadDistance = unloadDistance;
            Controller = controller;
        }

        public int UnloadDistance { get; }
        public string Controller { get; }
    }
}
#endif