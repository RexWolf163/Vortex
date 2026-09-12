using System.Collections.Generic;
using UnityEngine;
using Vortex.Core.Extensions.DefaultEnums;
using Vortex.Sdk.RebindSystem.Bus;
using Vortex.Unity.UI.Attributes;
using Vortex.Unity.UI.StateSwitcher;

namespace Vortex.Sdk.RebindSystem.Views
{
    /// <summary>
    /// Переключает свитчер по наличию отличий от заводских настроек: <see cref="SwitcherState.On"/> — есть
    /// (например, доступна кнопка «Сбросить всё»), <see cref="SwitcherState.Off"/> — всё заводское.
    /// </summary>
    public class ChangesStateHandler : MonoBehaviour
    {
        [SerializeField, StateSwitcher(typeof(SwitcherState))]
        private UIStateSwitcher switcher;

        private void OnEnable()
        {
            RebindBus.OnSlotsChanged += OnSlotsChanged;
            RebindBus.OnRebuilt += Refresh;
            RebindBus.OnGroupsChanged += Refresh;
            Refresh();
        }

        private void OnDisable()
        {
            RebindBus.OnSlotsChanged -= OnSlotsChanged;
            RebindBus.OnRebuilt -= Refresh;
            RebindBus.OnGroupsChanged -= Refresh;
        }

        private void OnSlotsChanged(IReadOnlyList<string> bindKeys) => Refresh();

        private void Refresh() =>
            switcher.Set(RebindBus.IsReady && RebindBus.Controller.HasChanges() ? SwitcherState.On : SwitcherState.Off);
    }
}
