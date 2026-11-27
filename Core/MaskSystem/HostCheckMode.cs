namespace Vortex.Core.MaskSystem
{
    /// <summary>Проверяется ли хост адреса и как.</summary>
    public enum HostCheckMode
    {
        /// <summary>Любой хост.</summary>
        Any = 0,

        /// <summary>Хост равен заданному.</summary>
        Exact = 1,

        /// <summary>Хост равен заданному или является его поддоменом по границе метки.</summary>
        WithSubdomains = 2
    }
}
