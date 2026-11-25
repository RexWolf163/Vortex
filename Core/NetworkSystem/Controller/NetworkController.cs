using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Vortex.Core.AppSystem.Bus;
using Vortex.Core.Extensions.ReactiveValues;
using Vortex.Core.MaskSystem;
using Vortex.Core.System.Abstractions;

namespace Vortex.Core.NetworkSystem
{
    /// <summary>
    /// Сетевой контроллер: единая точка запросов. Ведёт очередь и лимит, попытки, повторы, авторизацию, итоги и реестр.
    /// Драйвер (<see cref="INetworkDriver"/>) — прокладка к транспорту; назначается один раз при старте и передаёт
    /// настройки связи (<see cref="INetworkSettings"/>), которые валидируются при инициализации (ошибка — fail-fast).
    ///
    /// Запрос собирается цепочкой <see cref="Query"/> и запускается <see cref="Query.Send"/>; простые запросы —
    /// <see cref="Send(UrlData, object)"/> / <see cref="SendAsync(UrlData, object, CancellationToken)"/>.
    /// Только главный поток.
    /// </summary>
    public partial class NetworkController : SystemController<NetworkController, INetworkDriver>
    {
        /// <summary>Ключ owner-lock реактивных полей модуля.</summary>
        internal static readonly object OwnerKey = new();

        private static readonly Dictionary<string, TokenSource> TokenSources = new(StringComparer.Ordinal);
        private static readonly Random Jitter = new();

        private static INetworkSettings _settings;
        private static Dictionary<string, string> _hosts;
        private static bool _driverAssigned;
        private static long _lastQueryId;

        /// <summary>Реестр активных запросов.</summary>
        public static NetworkRegistry Registry { get; } = new();

        /// <summary>Глобальный выключатель сети. Выключена — новые запросы сразу получают итог «Сеть выключена».</summary>
        public static BoolData NetworkEnabled { get; } = new(true, OwnerKey);

        /// <summary>Подсказка драйвера о доступности сети. Запросы не блокирует.</summary>
        public static bool IsReachable => HasDriver() && Driver.IsReachable;

        /// <summary>Включить/выключить сеть.</summary>
        public static void SetNetworkEnabled(bool enabled) => NetworkEnabled.Set(enabled, OwnerKey);

        /// <summary>
        /// Зарегистрировать источник токена по ключу (ключ указан у провайдера авторизации в настройках).
        /// Повторная регистрация заменяет источник.
        /// </summary>
        public static void RegisterTokenSource(string key, TokenSource source)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("[Network] Token source key is empty.", nameof(key));
            TokenSources[key] = source ?? throw new ArgumentNullException(nameof(source));
        }

        public static void UnregisterTokenSource(string key) => TokenSources.Remove(key);

        #region Простые методы

        /// <summary>
        /// Простой запрос без результата (автоосвобождение). Метод — из настроек; для GET объект раскладывается в
        /// параметры адреса, иначе идёт телом через провайдер. Повторы — только для GET.
        /// </summary>
        public static void Send(UrlData url, object payload) => CreateSimple(url, payload).SetAutoRelease().Send();

        /// <summary>Простой запрос без результата со словарём параметров адреса.</summary>
        public static void Send(UrlData url, IReadOnlyDictionary<string, object> parameters) =>
            CreateSimple(url, parameters).SetAutoRelease().Send();

        /// <summary>Простой запрос с ожиданием: ждёт завершения, освобождает и возвращает <see cref="Query"/>.</summary>
        public static UniTask<Query> SendAsync(UrlData url, object payload, CancellationToken token = default) =>
            SendAndTakeAsync(CreateSimple(url, payload).SetCancellation(token));

        /// <summary>Простой запрос с ожиданием и словарём параметров адреса.</summary>
        public static UniTask<Query> SendAsync(UrlData url, IReadOnlyDictionary<string, object> parameters,
            CancellationToken token = default) =>
            SendAndTakeAsync(CreateSimple(url, parameters).SetCancellation(token));

        private static async UniTask<Query> SendAndTakeAsync(Query query)
        {
            try
            {
                query.Send();
            }
            catch
            {
                // Запрос ушёл в работу, но обработчик при запуске бросил: забрать его будет некому — освобождаем,
                // иначе он навсегда останется в реестре. Не запущен (ошибка проверки) — освобождать нечего.
                if (query.State.Value != QueryState.Draft)
                    query.Release();
                throw;
            }

            await query.Completion;
            return query.GetResult();
        }

        private static Query CreateSimple(UrlData url, object payload)
        {
            var query = new Query().SetUrl(url);
            if (payload != null)
                query.SetBody(payload);
            return query;
        }

        private static Query CreateSimple(UrlData url, IReadOnlyDictionary<string, object> parameters)
        {
            var query = new Query().SetUrl(url);
            if (parameters != null)
                query.AddParams(parameters);
            return query;
        }

        #endregion

        #region Драйвер и настройки

        protected override void OnDriverConnect()
        {
            if (_driverAssigned)
                throw new InvalidOperationException("[Network] Network driver is assigned once at startup; replacing it is not supported.");
            _driverAssigned = true;

            Driver.OnInit += OnDriverInitialized;
            App.OnExit += ExitAll;
        }

        protected override void OnDriverDisconnect()
        {
        }

        /// <summary>До выставления IsInit: получить и провалидировать настройки. Ошибка — fail-fast.</summary>
        private static void OnDriverInitialized()
        {
            Driver.OnInit -= OnDriverInitialized;
            var settings = Driver.Settings
                           ?? throw new InvalidOperationException("[Network] Driver provided no network settings.");
            _hosts = ValidateSettings(settings);
            _settings = settings;
            LogInfo($"Initialized. Environment '{settings.ActiveEnvironment}', hosts: {string.Join(", ", _hosts.Keys)}.");
        }

        private static Dictionary<string, string> ValidateSettings(INetworkSettings s)
        {
            if (s.Environments == null || s.Environments.Count == 0)
                throw Config("no environments defined");

            Dictionary<string, string> active = null;
            HashSet<string> referenceNames = null;
            var keys = new HashSet<string>(StringComparer.Ordinal);

            foreach (var environment in s.Environments)
            {
                if (environment == null || string.IsNullOrWhiteSpace(environment.key))
                    throw Config("environment with an empty key");
                if (!keys.Add(environment.key))
                    throw Config($"duplicate environment key '{environment.key}'");

                var hosts = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (var host in environment.hosts)
                {
                    if (host == null || string.IsNullOrWhiteSpace(host.name))
                        throw Config($"host with an empty name in environment '{environment.key}'");
                    if (hosts.ContainsKey(host.name))
                        throw Config($"duplicate host '{host.name}' in environment '{environment.key}'");
                    if (!Uri.TryCreate(host.url, UriKind.Absolute, out var uri)
                        || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                        throw Config($"host '{host.name}' in environment '{environment.key}' has an invalid http(s) URL '{host.url}'");
                    hosts.Add(host.name, host.url);
                }

                if (referenceNames == null)
                    referenceNames = new HashSet<string>(hosts.Keys, StringComparer.Ordinal);
                else if (!referenceNames.SetEquals(hosts.Keys))
                    throw Config($"environment '{environment.key}' has a different set of host names than the others");

                if (environment.key == s.ActiveEnvironment)
                    active = hosts;
            }

            if (string.IsNullOrWhiteSpace(s.ActiveEnvironment) || active == null)
                throw Config($"active environment '{s.ActiveEnvironment}' is empty or not found");

            if (!(s.DefaultTimeout > 0f) || float.IsInfinity(s.DefaultTimeout))
                throw Config("default timeout must be a finite positive number");
            if (s.MaxConcurrentRequests < 1) throw Config("max concurrent requests must be ≥ 1");
            if (s.MaxRequestBodyBytes <= 0 || s.MaxResponseBytes <= 0) throw Config("size limits must be positive");

            if (s.DefaultRetryPolicy == null) throw Config("default retry policy is missing");
            s.DefaultRetryPolicy.Validate("network settings");

            foreach (var mask in s.HttpAllowList)
                ValidateMask(mask, "http allow list");

            if (s.DefaultBodyProvider == null)
                throw Config("default body provider is empty or its type is missing");

            foreach (var rule in s.BodyProviderRules)
            {
                ValidateMask(rule.Mask, "body provider rules");
                if (rule.Value == null)
                    throw Config("body provider rule has an empty provider or its type is missing");
            }

            foreach (var rule in s.AuthRules)
            {
                ValidateMask(rule.Mask, "auth rules");
                if (rule.Value == null)
                    throw Config("auth rule has an empty provider or its type is missing");
                if (string.IsNullOrWhiteSpace(rule.Value.TokenSourceKey))
                    throw Config("auth provider has an empty token source key");
            }

            return active;
        }

        private static void ValidateMask(Mask mask, string where)
        {
            if (mask == null)
                throw Config($"empty mask in {where}");
            var validation = mask.Validate();
            if (!validation.IsValid)
                throw Config($"invalid mask '{mask.pattern}' in {where}: {validation.Message}");
        }

        private static Exception Config(string message) =>
            new InvalidOperationException("[Network] Invalid network settings: " + message + ".");

        #endregion
    }
}
