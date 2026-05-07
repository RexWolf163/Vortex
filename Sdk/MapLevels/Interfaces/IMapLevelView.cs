using Vortex.Sdk.MapLevels.Model;

namespace Vortex.Sdk.MapLevels.Interfaces
{
    public interface IMapLevelView
    {
        /// <summary>
        /// Возвращает список гейтов карты  
        /// </summary>
        /// <returns></returns>
        MapGate[] GetGates();
    }
}