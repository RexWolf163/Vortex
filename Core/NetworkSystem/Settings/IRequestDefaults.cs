using System.Collections.Generic;
using Vortex.Core.NetworkSystem.Model;
using Vortex.Core.NetworkSystem.Model.Enums;
using Vortex.Core.NetworkSystem.Providers;

namespace Vortex.Core.NetworkSystem.Settings
{
    /// <summary>
    /// Значения запроса по умолчанию: таймаут попытки, политика повторов, заголовки, метод простого запроса,
    /// провайдер тела и правила его выбора.
    /// </summary>
    public interface IRequestDefaults
    {
        /// <summary>Таймаут попытки, секунды.</summary>
        float DefaultTimeout { get; }

        /// <summary>Политика повторов.</summary>
        RetryPolicy DefaultRetryPolicy { get; }

        /// <summary>Заголовки «имя → значение», добавляемые к каждому запросу.</summary>
        IReadOnlyList<KeyValuePair<string, string>> DefaultHeaders { get; }

        /// <summary>Метод простого запроса.</summary>
        QueryMethod SimpleMethod { get; }

        /// <summary>Провайдер тела для адресов, не совпавших ни с одним правилом.</summary>
        BodyProvider DefaultBodyProvider { get; }

        /// <summary>Правила выбора провайдера тела, в порядке проверки.</summary>
        IReadOnlyList<BodyProviderRule> BodyProviderRules { get; }
    }
}
