#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using UnityEngine;
using Vortex.Core.LoggerSystem.Bus;
using Vortex.Core.LoggerSystem.Model;
using Vortex.Core.NetworkSystem;
using Vortex.Core.System.Abstractions;
using Random = System.Random;
using SettingsBus = Vortex.Core.SettingsSystem.Bus.Settings;

namespace Vortex.Unity.NetworkSystem.Mock
{
    /// <summary>
    /// Мок-драйвер: вместо сети отвечает правилами из <see cref="MockNetworkSettings"/>. Подкрутка из панели
    /// (<see cref="MockNetworkPanel"/>) живёт только в текущем запуске и в ассет не пишется.
    /// Приоритет: офлайн → принудительная ошибка → принудительный код (тело пустое) → правила → реакция по умолчанию.
    /// Существует только в редакторе и development-сборках.
    /// </summary>
    public class MockNetworkDriver : Singleton<MockNetworkDriver>, INetworkDriver
    {
        private const string LogSource = "Network.Mock";

        private static readonly IReadOnlyCollection<string> NoUnsettable = Array.Empty<string>();

        private readonly Random _random = new();
        private readonly HashSet<string> _disabledRules = new(StringComparer.Ordinal);
        private readonly HashSet<string> _enabledRules = new(StringComparer.Ordinal);
        private MockNetworkSettings _settings;

        /// <summary>Мок подключён к ядру и инициализирован.</summary>
        public static bool IsActive { get; private set; }

        #region Подкрутка (рантайм)

        internal bool Offline { get; set; }
        internal float LatencyMultiplier { get; set; } = 1f;
        internal int? ForcedStatus { get; set; }
        internal bool ForcedTransportError { get; set; }

        internal bool HasRule(string id)
        {
            foreach (var rule in _settings.Rules)
                if (rule.id == id)
                    return true;
            return false;
        }

        internal void SetRuleEnabled(string id, bool enabled)
        {
            (enabled ? _disabledRules : _enabledRules).Remove(id);
            (enabled ? _enabledRules : _disabledRules).Add(id);
        }

        internal void ResetOverrides()
        {
            Offline = false;
            LatencyMultiplier = 1f;
            ForcedStatus = null;
            ForcedTransportError = false;
            _disabledRules.Clear();
            _enabledRules.Clear();
        }

        #endregion

        public event Action OnInit;

        public INetworkSettings Settings => _settings;
        public bool IsReachable => !Offline;
        public bool SupportsDownloadToFile => true;
        public bool SupportsFileBodySegments => true;
        public bool SupportsUploadProgress => true;
        public bool SupportsDownloadProgress => true;
        public bool SupportsRedirectLimit => true;
        public IReadOnlyCollection<string> UnsettableHeaders => NoUnsettable;

        [RuntimeInitializeOnLoadMethod]
        private static void Run()
        {
            if (NetworkController.SetDriver(Instance))
                return;
            Dispose();
        }

        public void Init()
        {
            _settings = NetworkSettingsAsset.LoadSingle<MockNetworkSettings>();
            ValidateRules();
            ResetOverrides(); // подкрутка — только текущего подключения
            IsActive = true;
            OnInit?.Invoke();
        }

        public void Destroy()
        {
            IsActive = false;
            ResetOverrides();
        }

        public INetworkAttempt CreateAttempt(PreparedAttempt attempt)
        {
            var label = $"#{attempt.QueryId} attempt {attempt.AttemptNumber}";

            if (Offline)
                return Transport(attempt, "Mock: offline.", label + " → offline");
            if (ForcedTransportError)
                return Transport(attempt, "Mock: forced transport error.", label + " → forced transport error");
            if (ForcedStatus.HasValue)
            {
                Debug(label + $" → forced {ForcedStatus.Value}");
                return new MockAttempt(attempt, MockReactionKind.Response, ForcedStatus.Value, null, null, 0f, 0f, null);
            }

            var normalized = NetworkUrl.NormalizeForMasks(attempt.Url);
            foreach (var rule in _settings.Rules)
            {
                if (!IsRuleEnabled(rule) || (!rule.anyMethod && rule.method != attempt.Method) || !rule.mask.IsMatch(normalized))
                    continue;

                if (rule.failurePercent > 0 && _random.Next(100) < rule.failurePercent)
                    return Transport(attempt, $"Mock: random failure of rule '{rule.id}'.", label + $" → rule '{rule.id}' random failure");

                Debug(label + $" → rule '{rule.id}'");
                return Create(attempt, rule.ReactionFor(attempt.AttemptNumber));
            }

            Log.Print(LogLevel.Warning, $"[Network.Mock] No rule for {attempt.Method} {normalized}; default reaction.", LogSource);
            return Create(attempt, _settings.DefaultReaction);
        }

        private bool IsRuleEnabled(MockRule rule) =>
            _enabledRules.Contains(rule.id) || (rule.enabled && !_disabledRules.Contains(rule.id));

        private MockAttempt Create(PreparedAttempt attempt, MockReaction reaction) =>
            new(attempt, reaction.kind, reaction.statusCode, reaction.headers, reaction.GetBody(),
                reaction.delay * LatencyMultiplier, reaction.transferDuration * LatencyMultiplier,
                reaction.kind == MockReactionKind.TransportError ? "Mock: transport error." : null);

        private MockAttempt Transport(PreparedAttempt attempt, string message, string log)
        {
            Debug(log);
            return new MockAttempt(attempt, MockReactionKind.TransportError, 0, null, null, 0f, 0f, message);
        }

        private void ValidateRules()
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var rule in _settings.Rules)
            {
                if (rule == null || string.IsNullOrWhiteSpace(rule.id))
                    throw Invalid("rule with an empty id");
                if (!ids.Add(rule.id))
                    throw Invalid($"duplicate rule id '{rule.id}'");
                if (rule.mask == null)
                    throw Invalid($"rule '{rule.id}' has no mask");
                var validation = rule.mask.Validate();
                if (!validation.IsValid)
                    throw Invalid($"rule '{rule.id}' has an invalid mask '{rule.mask.pattern}': {validation.Message}");
                if (rule.reactions == null || rule.reactions.Count == 0)
                    throw Invalid($"rule '{rule.id}' has no reactions");
                if (rule.failurePercent is < 0 or > 100)
                    throw Invalid($"rule '{rule.id}' failure percent must be 0…100");
                foreach (var reaction in rule.reactions)
                    ValidateReaction(reaction, $"rule '{rule.id}'");
            }

            ValidateReaction(_settings.DefaultReaction, "default reaction");
        }

        private static void ValidateReaction(MockReaction reaction, string owner)
        {
            if (reaction == null)
                throw Invalid($"{owner}: empty reaction");
            if (reaction.kind == MockReactionKind.Response && (reaction.statusCode < 100 || reaction.statusCode > 599))
                throw Invalid($"{owner}: status code must be 100…599");
        }

        private static Exception Invalid(string message) =>
            new InvalidOperationException("[Network] Invalid mock settings: " + message + ".");

        private static void Debug(string message)
        {
            if (SettingsBus.Data()?.NetworkDebugMode ?? false)
                Log.Print(LogLevel.Common, "[Network.Mock] " + message, LogSource);
        }
    }
}
#endif
