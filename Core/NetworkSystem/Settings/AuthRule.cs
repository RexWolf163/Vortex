using Vortex.Core.MaskSystem;
using Vortex.Core.NetworkSystem.Providers;

namespace Vortex.Core.NetworkSystem.Settings
{
    /// <summary>
    /// Правило авторизации: маска адреса и провайдер авторизации, применяемый при совпадении.
    /// </summary>
    public sealed class AuthRule
    {
        /// <summary>Маска адреса запроса.</summary>
        public Mask Mask { get; }

        /// <summary>Провайдер авторизации для совпавших адресов.</summary>
        public AuthProvider Provider { get; }

        /// <summary>Создать правило.</summary>
        /// <param name="mask">Маска адреса запроса.</param>
        /// <param name="provider">Провайдер авторизации для совпавших адресов.</param>
        public AuthRule(Mask mask, AuthProvider provider)
        {
            Mask = mask;
            Provider = provider;
        }
    }
}
