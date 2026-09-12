using UnityEngine;
using Vortex.Core.Extensions.DefaultEnums;
using Vortex.Sdk.RebindSystem.Bus;
using Vortex.Sdk.RebindSystem.Model;
using Vortex.Unity.UI.Attributes;
using Vortex.Unity.UI.StateSwitcher;

namespace Vortex.Sdk.RebindSystem.Views
{
    /// <summary>
    /// Переключает свитчер по состоянию клапана перехвата: <see cref="SwitcherState.On"/> — ждём клавишу (для любого
    /// слота), <see cref="SwitcherState.Off"/> — не ждём.
    /// </summary>
    public class CaptureStateHandler : MonoBehaviour
    {
        [SerializeField, StateSwitcher(typeof(SwitcherState))]
        private UIStateSwitcher switcher;

        private void OnEnable()
        {
            RebindBus.OnCaptureChanged += OnCaptureChanged;
            OnCaptureChanged(RebindBus.Capture);
        }

        private void OnDisable() => RebindBus.OnCaptureChanged -= OnCaptureChanged;

        private void OnCaptureChanged(CaptureValve valve) =>
            switcher.Set(valve is { IsOpen: true } ? SwitcherState.On : SwitcherState.Off);
    }
}
