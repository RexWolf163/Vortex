using System.Collections.Generic;
using Vortex.Core.NetworkSystem.Model;

namespace Vortex.Core.NetworkSystem.Settings
{
    /// <summary>Куда обращаться: список окружений и ключ активного.</summary>
    public interface IEndpointSettings
    {
        /// <summary>Окружения. У всех одинаковый набор имён хостов.</summary>
        IReadOnlyList<NetworkEnvironment> Environments { get; }

        /// <summary>Ключ активного окружения.</summary>
        string ActiveEnvironmentKey { get; }
    }
}
