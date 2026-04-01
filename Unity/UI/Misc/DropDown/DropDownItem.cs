using System;
using UnityEngine;
using Vortex.Core.Extensions.DefaultEnums;
using Vortex.Core.System.Abstractions;
using Vortex.Core.System.Abstractions.ReactiveValues;
using Vortex.Unity.EditorTools.Attributes;
using Vortex.Unity.UI.UIComponents;

namespace Vortex.Unity.UI.Misc.DropDown
{
    /// <summary>
    /// Элемент выпадающего списка
    /// Может вызвать колбэк Select по своему номеру в списке
    /// </summary>
    public class DropDownItem : MonoBehaviour
    {
        [SerializeField] private UIComponent uiComponent;

        [SerializeField, ClassFilter(typeof(IDataStorage))]
        private MonoBehaviour dataStorage;

        private string _text;

        private Action<int> _selectCallback;

        private int _index;

        private DropDownListModel _model;

        private void OnEnable()
        {
            var storage = dataStorage as IDataStorage;
            _model = storage.GetData<DropDownListModel>(); //fail-fast
            _index = storage.GetData<IntData>();
            _text = _model.Texts[_index];
            _selectCallback = _model.SelectCallback;

            _model.OnUpdateData += Refresh;

            if (_selectCallback == null)
                Debug.LogError($"[DropDownItem] Selection callback is null for «{_text}» item");

            uiComponent.SetAction(Select);

            Refresh();
        }

        private void OnDisable()
        {
            if (_model != null)
                _model.OnUpdateData -= Refresh;
        }

        private void Refresh()
        {
            uiComponent.SetText(_text);
            uiComponent.SetSwitcher(_model.Current == _index ? SwitcherState.On : SwitcherState.Off);
        }

        private void Select()
        {
            _selectCallback?.Invoke(_index);
            _model.Current = _index;
            _model.CallOnUpdate();
            if (_model.CloseOnSelected)
                _model.CloseCallback?.Invoke();
        }
    }
}