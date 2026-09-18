namespace Vortex.Sdk.GallerySystem.View
{
    /// <summary>
    /// Режим показа закрытых (не помеченных ни одной из выбранных marks) карточек.
    /// </summary>
    public enum LockedMode
    {
        /// <summary>Закрытые не попадают в пул.</summary>
        Hide,

        /// <summary>Попадают, PoolItem получает isLocked=true. Визуальная трактовка — на дизайне префаба.</summary>
        ShowAsLocked
    }
}
