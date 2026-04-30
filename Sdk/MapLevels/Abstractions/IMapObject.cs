using Vortex.Core.Extensions.LogicExtensions.SerializationSystem;
using Vortex.Unity.Extensions.ReactiveValues;

namespace Vortex.Sdk.MapLevels.Abstractions
{
    /// <summary>
    /// Базовая абстракция для объектов карты
    /// </summary>
    [POCO]
    public interface IMapObject
    {
        /// <summary>
        /// Возвращает положение объекта на карте 
        /// </summary>
        /// <returns></returns>
        public Vector2Data Position { get; }
    }
}