namespace Vortex.Core.NetworkSystem.Config
{
    /// <summary>
    /// Ограничения нагрузки: число одновременных запросов, размер тела запроса, размер ответа.
    /// </summary>
    public interface ILimitSettings
    {
        /// <summary>Сколько запросов выполняется одновременно.</summary>
        int MaxConcurrent { get; }

        /// <summary>Наибольший размер тела запроса, байты.</summary>
        long MaxRequestBodySize { get; }

        /// <summary>Наибольший размер ответа, байты.</summary>
        long MaxResponseSize { get; }
    }
}
