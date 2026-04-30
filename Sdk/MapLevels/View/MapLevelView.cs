using UnityEngine;
using Vortex.Core.Extensions.DefaultEnums;
using Vortex.Sdk.MapLevels.Bus;
using Vortex.Sdk.MapLevels.Interfaces;
using Vortex.Sdk.MapLevels.Model;
using Vortex.Unity.DatabaseSystem.Attributes;
using Vortex.Unity.UI.Attributes;
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
    [DisallowMultipleComponent, RequireComponent(typeof(UIStateSwitcher))]
    public sealed class MapLevelView : MonoBehaviour, IMapLevelView
    {
        [SerializeField, DbRecord(typeof(MapLevelModel))]
        private string mapId;

        [SerializeField, StateSwitcher(typeof(SwitcherState))]
        private UIStateSwitcher switcher;

        /// <summary>
        /// Точки входа/спауна на карту
        /// </summary>
        [SerializeField] private MapGate[] gates = new MapGate[0];

        private bool _subscribed;

        private void OnEnable()
        {
            if (switcher == null)
            {
                Debug.LogError($"[MapLevelView] {name}: UIStateSwitcher не задан.");
                return;
            }

            MapLevelsBus.OnReady.Subscribe(TrySubscribe);
            MapLevelsBus.OnRelease += Unsubscribe;
            TrySubscribe();
        }

        private void OnDisable()
        {
            MapLevelsBus.OnReady.Unsubscribe(TrySubscribe);
            MapLevelsBus.OnRelease -= Unsubscribe;
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
            var state = activeGuid == mapId ? SwitcherState.On : SwitcherState.Off;
            switcher.Set(state);
        }

#if UNITY_EDITOR
        /// <summary>
        /// получить перечень точек входа/спауна
        /// </summary>
        /// <returns></returns>
        public MapGate[] GetGates() => gates;
#endif
    }
}