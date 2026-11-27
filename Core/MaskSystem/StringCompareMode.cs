namespace Vortex.Core.MaskSystem
{
    /// <summary>Правило сравнения строк, не зависящее от языка устройства.</summary>
    public enum StringCompareMode
    {
        /// <summary>Точное совпадение символов, без правил культуры.</summary>
        Ordinal = 0,

        /// <summary>По правилам инвариантной культуры.</summary>
        InvariantCulture = 1
    }
}
