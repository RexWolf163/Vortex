using Vortex.Core.Extensions.LogicExtensions.SerializationSystem;
using Vortex.Sdk.Core.GameCore;

namespace Vortex.Sdk.MapLevels.Model
{
    /// <summary>
    /// Часть GameModel для пакета MapLevels.
    /// Хранит первичные данные: текущий уровень игрока.
    /// Авторегистрируется в GameModel через ComplexModel-механизм.
    /// </summary>
    [POCO]
    public sealed class MapLevelsGameData : GameModel.IGameData
    {
        /// <summary>
        /// GUID уровня, на котором находится игрок.
        /// При NewGame пустая строка → контроллер выбирает первый из каталога.
        /// </summary>
        public string CurrentLevelGuid { get; set; } = string.Empty;
    }
}
