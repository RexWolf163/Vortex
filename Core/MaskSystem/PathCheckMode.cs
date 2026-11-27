namespace Vortex.Core.MaskSystem
{
    /// <summary>Проверяется ли путь адреса и как.</summary>
    public enum PathCheckMode
    {
        /// <summary>Любой путь.</summary>
        Any = 0,

        /// <summary>Путь равен заданному.</summary>
        Exact = 1,

        /// <summary>Путь начинается с заданного по границе сегмента.</summary>
        Prefix = 2
    }
}
