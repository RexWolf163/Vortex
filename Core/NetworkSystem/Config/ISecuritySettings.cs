using System.Collections.Generic;
using Vortex.Core.MaskSystem;

namespace Vortex.Core.NetworkSystem.Config
{
    /// <summary>
    /// Защита: разрешённые http-хосты, правила авторизации, следование редиректам с авторизацией,
    /// имена заголовков и параметров, скрываемых в логах, длина тела в логе.
    /// </summary>
    public interface ISecuritySettings
    {
        /// <summary>Маски адресов, для которых разрешён http. Остальные адреса — только https.</summary>
        IReadOnlyList<Mask> AllowedHttpHosts { get; }

        /// <summary>Правила авторизации, в порядке проверки. Адрес без совпадения идёт без авторизации.</summary>
        IReadOnlyList<AuthRule> AuthRules { get; }

        /// <summary>
        /// Следовать ли редиректам в запросах с авторизацией. <c>false</c> — переход не выполняется,
        /// токен на новый адрес не уходит.
        /// </summary>
        bool FollowAuthRedirects { get; }

        /// <summary>Имена заголовков и параметров, значения которых скрываются в логах.</summary>
        IReadOnlyList<string> MaskedNames { get; }

        /// <summary>Наибольшая длина тела запроса и ответа в логе, символы.</summary>
        int LogBodyLength { get; }
    }
}
