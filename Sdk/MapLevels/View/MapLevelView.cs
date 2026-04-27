using UnityEngine;
using Vortex.Core.Extensions.DefaultEnums;
using Vortex.Sdk.MapLevels.Bus;
using Vortex.Sdk.MapLevels.Presets;
using Vortex.Unity.UI.StateSwitcher;

namespace Vortex.Sdk.MapLevels.View
{
    /// <summary>
    /// Представление одного уровня карты на корне префаба.
    /// Подписывается на ActiveLevelGuid из шины и переключает локальный UIStateSwitcher
    /// между SwitcherState.On (своя группа активна) и SwitcherState.Off.
    ///
    /// Профильный View — читает шину MapLevelsBus напрямую (доменно связан).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MapLevelView : MonoBehaviour
    {
        [SerializeField] private MapLevelPreset preset;
        [SerializeField] private UIStateSwitcher switcher;

        private bool _subscribed;

        private void OnEnable()
        {
            if (preset == null)
            {
                Debug.LogError($"[MapLevelView] {name}: preset не задан.");
                return;
            }

            if (switcher == null)
            {
                Debug.LogError($"[MapLevelView] {name}: UIStateSwitcher не задан.");
                return;
            }

            MapLevelsBus.OnReady    += TrySubscribe;
            MapLevelsBus.OnRelease  += Unsubscribe;
            TrySubscribe();
        }

        private void OnDisable()
        {
            MapLevelsBus.OnReady    -= TrySubscribe;
            MapLevelsBus.OnRelease  -= Unsubscribe;
            Unsubscribe();
        }

        private void TrySubscribe()
        {
            if (_subscribed) return;
            if (!MapLevelsBus.IsReady) return;

            MapLevelsBus.Data.ActiveLevelGuid.OnUpdate += OnActiveChanged;
            _subscribed = true;

            Apply(MapLevelsBus.Data.ActiveLevelGuid.Value);
        }

        private void Unsubscribe()
        {
            if (!_subscribed) return;
            if (MapLevelsBus.Data != null)
                MapLevelsBus.Data.ActiveLevelGuid.OnUpdate -= OnActiveChanged;
            _subscribed = false;
        }

        private void OnActiveChanged(string activeGuid) => Apply(activeGuid);

        private void Apply(string activeGuid)
        {
            var state = activeGuid == preset.GuidPreset ? SwitcherState.On : SwitcherState.Off;
            switcher.Set(state);
        }
    }
}
