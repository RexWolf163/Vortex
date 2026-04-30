using Vortex.Core.Extensions.LogicExtensions.SerializationSystem;

namespace Vortex.Sdk.MapLevels.Model
{
    /// <summary>
    /// Модель данных текущего представления данных,
    /// пригодная для сериализации в сейв.
    ///
    /// При инициализации(загрузки) уровня в режиме "Входа в локацию",
    /// Все объекты и NPC загружаются согласно правилам инициализации.
    /// Эта модель является производной.
    /// 
    /// Но при загрузке уровня в режиме LoadGame,эта модель становится источником данных 
    /// </summary>
    [POCO]
    public class MapLevelViewModel
    {
    }
}