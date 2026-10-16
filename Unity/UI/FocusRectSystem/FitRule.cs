namespace Vortex.Unity.UI.FocusRectSystem
{
    /// <summary>
    /// Правило вписывания фрагмента в родителя View (по аналогии с AspectRatioFitter)
    /// </summary>
    public enum FitRule
    {
        /// <summary>
        /// Фрагмент покрывает родителя целиком, лишнее за краями обрезается
        /// </summary>
        Envelope,

        /// <summary>
        /// Фрагмент целиком виден внутри родителя, по одной из осей остаются поля
        /// </summary>
        FitParent
    }
}
