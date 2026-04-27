namespace Vortex.Sdk.MapLevels.Model
{
    /// <summary>
    /// Состояние контейнера уровня карты в runtime.
    /// </summary>
    public enum MapContainerState
    {
        /// <summary>
        /// Контейнер пуст: префаб не инстанцирован.
        /// </summary>
        Empty,

        /// <summary>
        /// Идёт инстанциация префаба.
        /// </summary>
        Loading,

        /// <summary>
        /// Префаб инстанцирован и готов к использованию.
        /// </summary>
        Loaded,

        /// <summary>
        /// Идёт уничтожение инстанса.
        /// </summary>
        Unloading
    }
}
