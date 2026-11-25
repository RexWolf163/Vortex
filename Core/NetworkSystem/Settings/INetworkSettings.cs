using System;
using System.Collections.Generic;
using Vortex.Core.MaskSystem;

namespace Vortex.Core.NetworkSystem
{
    /// <summary>
    /// Настройки связи — всё, что контроллер берёт из конфигурации. Драйвер при инициализации передаёт контроллеру
    /// свой ассет как этот интерфейс. Интерфейс отдаёт данные; выбор окружения, первое совпадение по маскам,
    /// нормализацию URL и валидацию выполняет контроллер. В рантайме настройки неизменны.
    /// </summary>
    public interface INetworkSettings
    {
        /// <summary>Окружения: ключ → набор адресов хостов. У всех окружений одинаковый набор имён хостов.</summary>
        IReadOnlyList<NetworkEnvironment> Environments { get; }

        /// <summary>Ключ активного окружения.</summary>
        string ActiveEnvironment { get; }

        /// <summary>Таймаут одной попытки по умолчанию, секунды.</summary>
        float DefaultTimeout { get; }

        /// <summary>Политика повторов по умолчанию. Контроллер выдаёт запросам копии.</summary>
        RetryPolicy DefaultRetryPolicy { get; }

        /// <summary>Заголовки по умолчанию.</summary>
        IReadOnlyList<NetworkHeader> DefaultHeaders { get; }

        /// <summary>Автоматический User-Agent (приложение, версия, платформа). Заголовок по умолчанию его переопределяет.</summary>
        string UserAgent { get; }

        /// <summary>Лимит одновременно выполняющихся запросов.</summary>
        int MaxConcurrentRequests { get; }

        /// <summary>Лимит размера тела запроса, байты.</summary>
        long MaxRequestBodyBytes { get; }

        /// <summary>Лимит размера ответа, байты.</summary>
        long MaxResponseBytes { get; }

        /// <summary>Сколько символов тела выводить в лог.</summary>
        int MaxLoggedBodyChars { get; }

        /// <summary>Маски хостов, которым разрешён http (адрес нормализован, без параметров).</summary>
        IReadOnlyList<Mask> HttpAllowList { get; }

        /// <summary>Имена заголовков и параметров, значения которых маскируются в логах.</summary>
        IReadOnlyList<string> MaskedLogKeys { get; }

        /// <summary>Метод простых запросов.</summary>
        HttpMethod SimpleMethod { get; }

        /// <summary>Провайдер тела по умолчанию.</summary>
        IBodyProvider DefaultBodyProvider { get; }

        /// <summary>Исключения «маска → провайдер тела», сверху вниз до первого совпадения.</summary>
        IReadOnlyList<MaskBinding<IBodyProvider>> BodyProviderRules { get; }

        /// <summary>Белый список «маска → провайдер авторизации». Нет совпадения — запрос без авторизации.</summary>
        IReadOnlyList<MaskBinding<IAuthProvider>> AuthRules { get; }
    }

    /// <summary>Окружение: ключ и набор адресов хостов.</summary>
    [Serializable]
    public class NetworkEnvironment
    {
        public string key;
        public List<NetworkHost> hosts = new();
    }

    /// <summary>Именованный хост: имя → адрес (основа URL).</summary>
    [Serializable]
    public class NetworkHost
    {
        public string name;
        public string url;
    }

    /// <summary>Заголовок: имя → значение.</summary>
    [Serializable]
    public class NetworkHeader
    {
        public string name;
        public string value;
    }

    /// <summary>Пара «маска → значение» из списка правил.</summary>
    public readonly struct MaskBinding<T>
    {
        public Mask Mask { get; }
        public T Value { get; }

        public MaskBinding(Mask mask, T value)
        {
            Mask = mask;
            Value = value;
        }
    }
}
