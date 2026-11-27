namespace Vortex.Core.MaskSystem
{
    /// <summary>
    /// С каких краёв копии входа убираются пробельные символы (по <see cref="char.IsWhiteSpace(char)"/>)
    /// перед сравнением.
    /// </summary>
    public enum TrimMode
    {
        /// <summary>Не обрезать.</summary>
        None = 0,

        /// <summary>Убрать пробельные символы в начале.</summary>
        TrimStart = 1,

        /// <summary>Убрать пробельные символы в конце.</summary>
        TrimEnd = 2,

        /// <summary>Убрать пробельные символы с обоих краёв.</summary>
        FullTrim = 3
    }
}
