namespace Vortex.Core.MaskSystem
{
    /// <summary>Проверяется ли порт адреса. Порт по умолчанию схемы равен явно указанному.</summary>
    public enum PortCheckMode
    {
        /// <summary>Любой порт.</summary>
        Any = 0,

        /// <summary>Порт равен заданному.</summary>
        Exact = 1
    }
}
