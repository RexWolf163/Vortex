namespace Vortex.Core.StateValve
{
    /// <summary>Режим свёртки створок клапана (<see cref="StateValve"/>) в общий булев итог.</summary>
    public enum ValveMode
    {
        /// <summary>Открыт, если открыты ВСЕ створки.</summary>
        And,

        /// <summary>Открыт, если открыта ХОТЬ ОДНА створка.</summary>
        Or,

        /// <summary>Открыт, если открыта РОВНО ОДНА створка (ноль или ≥2 открытых → закрыт).</summary>
        Xor
    }
}
