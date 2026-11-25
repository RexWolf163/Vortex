using Vortex.Unity.Extensions.Abstractions;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using Vortex.Core.LoggerSystem.Bus;
using Vortex.Core.LoggerSystem.Model;
#endif

namespace Vortex.Unity.NetworkSystem.Mock
{
    /// <summary>
    /// Панель подкрутки мок-драйвера: публичные методы для UnityEvent (кнопки UIComponent + SetActionComponent).
    /// Класс существует в любой сборке (нет Missing Script), но логика — только в редакторе и development-сборках;
    /// в релизе методы пустые. Если активный драйвер — не мок, методы пишут предупреждение и ничего не меняют.
    /// Подкрутка живёт только в текущем запуске.
    /// </summary>
    public class MockNetworkPanel : MonoBehaviourSingleton<MockNetworkPanel>
    {
        /// <summary>Включить/выключить офлайн: все запросы получают транспортную ошибку.</summary>
        public void SetOffline(bool offline)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (TryGetDriver(out var driver))
                driver.Offline = offline;
#endif
        }

        public void ToggleOffline()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (TryGetDriver(out var driver))
                driver.Offline = !driver.Offline;
#endif
        }

        /// <summary>Множитель задержек и длительностей передачи (1 — как в правилах).</summary>
        public void SetLatencyMultiplier(float multiplier)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (TryGetDriver(out var driver))
                driver.LatencyMultiplier = multiplier < 0f ? 0f : multiplier;
#endif
        }

        public void EnableRule(string id)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            SetRule(id, true);
#endif
        }

        public void DisableRule(string id)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            SetRule(id, false);
#endif
        }

        /// <summary>Все запросы получают этот HTTP-код с пустым телом.</summary>
        public void ForceStatus(int code)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!TryGetDriver(out var driver))
                return;
            if (code < 100 || code > 599)
            {
                Warn($"ForceStatus: code {code} is outside 100…599; ignored.");
                return;
            }

            driver.ForcedStatus = code;
#endif
        }

        /// <summary>Все запросы получают транспортную ошибку.</summary>
        public void ForceTransportError()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (TryGetDriver(out var driver))
                driver.ForcedTransportError = true;
#endif
        }

        /// <summary>Снять принудительный код и принудительную ошибку.</summary>
        public void ClearForced()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!TryGetDriver(out var driver))
                return;
            driver.ForcedStatus = null;
            driver.ForcedTransportError = false;
#endif
        }

        /// <summary>Сбросить всю подкрутку к значениям из ассета.</summary>
        public void ResetAll()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (TryGetDriver(out var driver))
                driver.ResetOverrides();
#endif
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private void SetRule(string id, bool enabled)
        {
            if (!TryGetDriver(out var driver))
                return;
            if (!driver.HasRule(id))
            {
                Warn($"unknown mock rule '{id}'.");
                return;
            }

            driver.SetRuleEnabled(id, enabled);
        }

        private bool TryGetDriver(out MockNetworkDriver driver)
        {
            driver = MockNetworkDriver.IsActive ? MockNetworkDriver.Instance : null;
            if (driver != null)
                return true;
            Warn("the active network driver is not the mock; nothing changed.");
            return false;
        }

        private void Warn(string message) =>
            Log.Print(LogLevel.Warning, $"[Network.Mock] {name}: {message}", this);
#endif
    }
}
