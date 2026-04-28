using Vortex.Sdk.MapLevels.Model;

namespace Vortex.Sdk.MapLevels.Interfaces
{
    public interface IMapLevelView
    {
#if UNITY_EDITOR
        /// <summary>
        /// Возвращает список гейтов карты  
        /// </summary>
        /// <returns></returns>
        MapGate[] GetGates();

#endif
    }
}