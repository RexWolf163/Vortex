namespace Vortex.Core.NetworkSystem.Model.Enums
{
    /// <summary>Чем задано условие повтора.</summary>
    public enum RetryConditionKind
    {
        /// <summary>Категорией итога.</summary>
        Outcome = 0,

        /// <summary>Диапазоном HTTP-кодов.</summary>
        StatusRange = 1
    }
}
