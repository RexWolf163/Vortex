using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using Vortex.Core.UIProviderSystem.Bus;
using Vortex.Unity.UI.Attributes;
using Vortex.Unity.UI.Misc.DropDown;
using Vortex.Unity.UI.StateSwitcher;
using Vortex.Unity.UIProviderSystem.View;

namespace Vortex.Sdk.Core.RollbackSettingsHandlers
{
    /// <summary>
    /// Сохранение/откат изменений настроек
    /// </summary>
    public class RollbackSettings : MonoBehaviour
    {
        private enum WorkState
        {
            Normal,
            HasChanges,
            RollbackRequest,
        }

        private readonly Dictionary<DropDownComponent, int> _dropDownsIndex = new();

        private readonly Dictionary<Slider, float> _slidersIndex = new();

        [SerializeField] private UserInterface ui;

        [SerializeField, StateSwitcher(typeof(WorkState))]
        private UIStateSwitcher switcher;

        private bool _hasChanges;
        private bool _requested;


        private void OnEnable()
        {
            _hasChanges = false;
            _requested = false;
            switcher.Set(WorkState.Normal);
        }

        private void OnDisable()
        {
            RestoreSettings();
        }

        public void SaveSettings()
        {
            var ddList = _dropDownsIndex.Keys.ToList();
            foreach (var component in ddList)
                _dropDownsIndex[component] = component.GetValue();

            var slidersList = _slidersIndex.Keys.ToList();
            foreach (var component in slidersList)
                _slidersIndex[component] = component.value;
            _hasChanges = false;
            _requested = false;
            switcher.Set(WorkState.Normal);
        }

        public void RestoreSettings()
        {
            foreach (var component in _dropDownsIndex.Keys)
                component.SetValue(_dropDownsIndex[component]);

            foreach (var component in _slidersIndex.Keys)
                component.value = _slidersIndex[component];
            _hasChanges = false;
            _requested = false;
            switcher.Set(WorkState.Normal);
        }

        public void Link(DropDownComponent dropdown) => _dropDownsIndex[dropdown] = dropdown.GetValue();

        public void Link(Slider slider) => _slidersIndex[slider] = slider.value;

        public void CallExit()
        {
            _requested = _hasChanges;
            if (_requested)
                switcher.Set(WorkState.RollbackRequest);
            else
            {
                UIProvider.Close(ui.GetId());
                _requested = false;
            }
        }

        public bool HasChanges()
        {
            foreach (var component in _dropDownsIndex.Keys)
                if (component.GetValue() != _dropDownsIndex[component])
                    return true;

            foreach (var component in _slidersIndex.Keys)
                if (!Mathf.Approximately(component.value, _slidersIndex[component]))
                    return true;
            return false;
        }

        /// <summary>
        /// Обработка состояний в Update, так как параметров не может быть очень много
        /// а городить сложные реактивные проверки будет гораздо сложнее 
        /// </summary>
        private void Update()
        {
            if (_hasChanges) return;
            _hasChanges = HasChanges();

            if (_hasChanges && !_requested)
                switcher.Set(WorkState.HasChanges);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (ui != null) return;
            ui = GetComponentInParent<UserInterface>();
        }
#endif
    }
}