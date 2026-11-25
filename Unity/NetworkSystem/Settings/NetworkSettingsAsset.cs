using System.Collections.Generic;
using System.Linq;
using Sirenix.OdinInspector;
using UnityEngine;
using Vortex.Core.MaskSystem;
using Vortex.Core.NetworkSystem;
using Vortex.Unity.NetworkSystem.Providers;

namespace Vortex.Unity.NetworkSystem
{
    /// <summary>
    /// Базовый ассет настроек связи: реализует <see cref="INetworkSettings"/>, содержит общие поля и визуальный сахар
    /// инспектора. Ассеты драйверов наследуют его и добавляют параметры своего транспорта; драйвер при инициализации
    /// передаёт свой ассет контроллеру. В рантайме ассет неизменен; валидацию выполняет контроллер.
    /// </summary>
    public abstract class NetworkSettingsAsset : ScriptableObject, INetworkSettings
    {
        private const long Megabyte = 1024L * 1024L;

        [BoxGroup("Окружения")]
        [SerializeField, ListDrawerSettings(ShowFoldout = true)]
        [InfoBox("У всех окружений одинаковый набор имён хостов; различаются адреса.")]
        private List<NetworkEnvironment> environments = new() { new NetworkEnvironment { key = "prod" } };

        [BoxGroup("Окружения")]
        [SerializeField, ValueDropdown(nameof(GetEnvironmentKeys))]
        private string activeEnvironment = "prod";

        [BoxGroup("Запросы")]
        [SerializeField, Min(0.1f), SuffixLabel("с", true)]
        private float defaultTimeout = 30f;

        [BoxGroup("Запросы")]
        [SerializeField, Min(1)]
        private int maxConcurrentRequests = 4;

        [BoxGroup("Запросы")]
        [SerializeField, Tooltip("Метод простых запросов. Повторы у простых запросов — только для GET.")]
        private HttpMethod simpleMethod = HttpMethod.Post;

        [BoxGroup("Запросы")]
        [SerializeField]
        private List<NetworkHeader> defaultHeaders = new();

        [BoxGroup("Запросы")]
        [SerializeField, HideLabel, Title("Политика повторов по умолчанию", bold: false)]
        private RetryPolicy defaultRetryPolicy = new();

        [BoxGroup("Лимиты")]
        [SerializeField, Min(1), SuffixLabel("МБ", true)]
        private int maxRequestBodyMegabytes = 10;

        [BoxGroup("Лимиты")]
        [SerializeField, Min(1), SuffixLabel("МБ", true)]
        private int maxResponseMegabytes = 20;

        [BoxGroup("Безопасность и логи")]
        [SerializeField, Tooltip("Хосты, которым разрешён http. Проверяется адрес вида схема://хост[:порт]/путь.")]
        private List<HttpAllowEntry> httpAllowList = new();

        [BoxGroup("Безопасность и логи")]
        [SerializeField, Tooltip("Имена заголовков и параметров, значения которых скрываются в логах.")]
        private List<string> maskedLogKeys = new() { "Authorization" };

        [BoxGroup("Безопасность и логи")]
        [SerializeField, Min(0)]
        private int maxLoggedBodyChars = 1024;

        [BoxGroup("Провайдеры тела")]
        [SerializeReference]
        private IBodyProvider defaultBodyProvider = new JsonUtilityBodyProvider();

        [BoxGroup("Провайдеры тела")]
        [SerializeField, Tooltip("Исключения: сверху вниз до первого совпадения маски, иначе провайдер по умолчанию.")]
        private List<BodyProviderRule> bodyProviderRules = new();

        [BoxGroup("Авторизация")]
        [SerializeField, Tooltip("Белый список: сверху вниз до первого совпадения; нет совпадения — без авторизации.")]
        private List<AuthRule> authRules = new();

        [System.NonSerialized] private List<Mask> _httpAllowCache;
        [System.NonSerialized] private List<MaskBinding<IBodyProvider>> _bodyRulesCache;
        [System.NonSerialized] private List<MaskBinding<IAuthProvider>> _authRulesCache;

        public IReadOnlyList<NetworkEnvironment> Environments => environments;
        public string ActiveEnvironment => activeEnvironment;
        public float DefaultTimeout => defaultTimeout;
        public RetryPolicy DefaultRetryPolicy => defaultRetryPolicy;
        public IReadOnlyList<NetworkHeader> DefaultHeaders => defaultHeaders;

        public string UserAgent =>
            $"{Application.productName}/{Application.version} ({Application.platform}; Unity {Application.unityVersion})";

        public int MaxConcurrentRequests => maxConcurrentRequests;
        public long MaxRequestBodyBytes => maxRequestBodyMegabytes * Megabyte;
        public long MaxResponseBytes => maxResponseMegabytes * Megabyte;
        public int MaxLoggedBodyChars => maxLoggedBodyChars;
        public IReadOnlyList<string> MaskedLogKeys => maskedLogKeys;
        public HttpMethod SimpleMethod => simpleMethod;
        public IBodyProvider DefaultBodyProvider => defaultBodyProvider;

        public IReadOnlyList<Mask> HttpAllowList =>
            _httpAllowCache ??= httpAllowList.Select(e => e?.mask).ToList();

        public IReadOnlyList<MaskBinding<IBodyProvider>> BodyProviderRules =>
            _bodyRulesCache ??= bodyProviderRules
                .Select(r => new MaskBinding<IBodyProvider>(r?.mask, r?.provider)).ToList();

        public IReadOnlyList<MaskBinding<IAuthProvider>> AuthRules =>
            _authRulesCache ??= authRules
                .Select(r => new MaskBinding<IAuthProvider>(r?.mask, r?.provider)).ToList();

        protected virtual void OnValidate()
        {
            _httpAllowCache = null;
            _bodyRulesCache = null;
            _authRulesCache = null;
        }

        private IEnumerable<string> GetEnvironmentKeys() =>
            environments.Where(e => e != null && !string.IsNullOrWhiteSpace(e.key)).Select(e => e.key);
    }
}
