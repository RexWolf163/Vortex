namespace Vortex.Core.Extensions.LogicExtensions
{
    /// <summary>
    /// Класс собирающий исключения для CopyFrom (платформозависимая логика): типы статических полей,
    /// объявленных в partial-частях платформенного слоя. Свойства, объявленные в этих типах и их предках,
    /// не копируются — это служебные свойства платформенного базового класса, а не доменные данные.
    /// </summary>
    public static partial class CopyExclusionMarker
    {
    }
}
