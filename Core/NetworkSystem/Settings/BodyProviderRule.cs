using Vortex.Core.MaskSystem;
using Vortex.Core.NetworkSystem.Providers;

namespace Vortex.Core.NetworkSystem.Settings
{
    /// <summary>Правило выбора провайдера тела: маска адреса и провайдер, применяемый при совпадении.</summary>
    public sealed class BodyProviderRule
    {
        /// <summary>Маска адреса запроса.</summary>
        public Mask Mask { get; }

        /// <summary>Провайдер тела для совпавших адресов.</summary>
        public BodyProvider Provider { get; }

        /// <summary>Создать правило.</summary>
        /// <param name="mask">Маска адреса запроса.</param>
        /// <param name="provider">Провайдер тела для совпавших адресов.</param>
        public BodyProviderRule(Mask mask, BodyProvider provider)
        {
            Mask = mask;
            Provider = provider;
        }
    }
}
