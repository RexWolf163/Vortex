namespace Vortex.Unity.UI.FocusRectSystem
{
    /// <summary>
    /// Канал, через который View применяет коэффициент растяжения
    /// </summary>
    public enum FitMethod
    {
        /// <summary>
        /// Размер View = размер родителя × k, масштаб 1
        /// </summary>
        SizeDelta,

        /// <summary>
        /// Размер View = размер родителя, масштаб k
        /// </summary>
        Scale
    }
}
