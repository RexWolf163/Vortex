using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using Vortex.Unity.UI.Misc.DropDown;

namespace Vortex.Unity.UI.RollbackSystem.Sources
{
    /// <summary>
    /// Источник отката UI-контролов: выпадающие списки и слайдеры. Повторяет поведение <c>RollbackSettings</c>:
    /// точка — индексы списков и значения слайдеров, откат выставляет значения обратно в контролы, а контролы
    /// сами пишут их в настройки.
    ///
    /// Контролы не задаются списком: каждый регистрируется маркером <see cref="RollbackControl"/>, который
    /// лежит на самом контроле и переезжает вместе с ним при копировании. Значение запоминается в момент
    /// регистрации; фиксация точки хэндлером перезаписывает значения всех зарегистрированных контролов.
    ///
    /// Изменение замечается по событиям контролов (выбор в списке или SetValue, onValueChanged слайдера);
    /// изменение значения кодом в обход этих событий не видно.
    /// </summary>
    [Serializable]
    public class UIControlsRollback : RollbackSource
    {
        private readonly Dictionary<DropDownComponent, int> _dropdowns = new();
        private readonly Dictionary<Slider, float> _sliders = new();

        /// <summary>Зарегистрировать выпадающий список. Повторная регистрация ничего не меняет.</summary>
        public void Link(DropDownComponent dropdown)
        {
            if (ReferenceEquals(dropdown, null) || _dropdowns.ContainsKey(dropdown))
                return;
            _dropdowns[dropdown] = dropdown.GetValue();
            if (IsInitialized)
                dropdown.OnValueSelected += OnDropdownSelected;
        }

        /// <summary>Зарегистрировать слайдер. Повторная регистрация ничего не меняет.</summary>
        public void Link(Slider slider)
        {
            if (ReferenceEquals(slider, null) || _sliders.ContainsKey(slider))
                return;
            _sliders[slider] = slider.value;
            if (IsInitialized)
                slider.onValueChanged.AddListener(OnSliderChanged);
        }

        public void Unlink(DropDownComponent dropdown)
        {
            if (ReferenceEquals(dropdown, null) || !_dropdowns.Remove(dropdown))
                return;
            if (IsInitialized && dropdown != null)
                dropdown.OnValueSelected -= OnDropdownSelected;
            Refresh();
        }

        public void Unlink(Slider slider)
        {
            if (ReferenceEquals(slider, null) || !_sliders.Remove(slider))
                return;
            if (IsInitialized && slider != null)
                slider.onValueChanged.RemoveListener(OnSliderChanged);
            Refresh();
        }

        protected override void Subscribe()
        {
            foreach (var dropdown in _dropdowns.Keys)
                dropdown.OnValueSelected += OnDropdownSelected;
            foreach (var slider in _sliders.Keys)
                slider.onValueChanged.AddListener(OnSliderChanged);
        }

        protected override void Unsubscribe()
        {
            foreach (var dropdown in _dropdowns.Keys)
                if (dropdown != null)
                    dropdown.OnValueSelected -= OnDropdownSelected;
            foreach (var slider in _sliders.Keys)
                if (slider != null)
                    slider.onValueChanged.RemoveListener(OnSliderChanged);
        }

        protected override void MakeCheckpoint()
        {
            // Снимок ключей: перезапись значения во время обхода словаря бросает исключение в Mono.
            foreach (var dropdown in _dropdowns.Keys.ToList())
                if (dropdown != null)
                    _dropdowns[dropdown] = dropdown.GetValue();
            foreach (var slider in _sliders.Keys.ToList())
                if (slider != null)
                    _sliders[slider] = slider.value;
        }

        protected override bool DiffersFromCheckpoint()
        {
            foreach (var pair in _dropdowns)
                if (pair.Key != null && pair.Key.GetValue() != pair.Value)
                    return true;
            foreach (var pair in _sliders)
                if (pair.Key != null && !Mathf.Approximately(pair.Key.value, pair.Value))
                    return true;
            return false;
        }

        protected override void Restore()
        {
            foreach (var pair in _dropdowns)
                // -1 — список был пуст: выставлять нечего.
                if (pair.Key != null && pair.Value >= 0)
                    pair.Key.SetValue(pair.Value);
            foreach (var pair in _sliders)
                if (pair.Key != null)
                    pair.Key.value = pair.Value;
        }

        private void OnDropdownSelected(int index) => Refresh();

        private void OnSliderChanged(float value) => Refresh();
    }
}
