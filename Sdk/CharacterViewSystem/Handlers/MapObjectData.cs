using Vortex.Core.Extensions.ReactiveValues;
using Vortex.Sdk.MapLevels.Abstractions;

namespace AppScripts.CharacterViewSystem.Handlers
{
    /// <summary>
    /// Реактивный контейнер для хранения данных объекта карты, например как цели персонажа  
    /// </summary>
    public class MapObjectData : ReactiveValue<IMapObject>
    {
        public MapObjectData(IMapObject value) => Value = value;

        public MapObjectData(IMapObject value, object owner)
        {
            Value = value;
            _owner = owner;
        }
    }
}