using System;
using UnityEngine;
using UnityEngine.UI;
using Vortex.Core.System.Abstractions.ReactiveValues;
using Vortex.Unity.UI.PoolSystem;

namespace Vortex.Unity.UI.Misc.DropDown
{
    public class DropDownList : MonoBehaviour
    {
        [SerializeField] private Pool pool;

        [SerializeField] private ScrollRect scrollRect;

        private DropDownListModel _model;

        private string _listHash;

        public void Set(string[] text, Action<int> select, Action closeList, int selectedIndex = 0,
            bool closeOnSelected = false, int scrollSensitivity = 1)
        {
            if (text == null || text.Length == 0)
            {
                Debug.LogError("[DropDownList] Call on empty list.");
                Destroy(gameObject);
                return;
            }

            var newHash = string.Join(";", text);
            if (newHash.Equals(_listHash))
            {
                _model.Current = selectedIndex;
                return;
            }

            _model?.Dispose();
            _model = new DropDownListModel(select, closeList, text, closeOnSelected, selectedIndex)
            {
                ScrollSensitivity = scrollSensitivity
            };
            _listHash = newHash;

            OnEnable();
        }

        private void OnEnable()
        {
            if (_model == null)
                return;

            pool.Clear();
            var l = _model.Texts.Count;
            for (var i = 0; i < l; i++)
                pool.AddItem(_model, new IntData(i));

            if (scrollRect == null) return;

            scrollRect.scrollSensitivity = _model.ScrollSensitivity;
            var v = (float)_model.Current / (l - 1);
            scrollRect.normalizedPosition =
                new Vector2(scrollRect.horizontal ? 1 - v : 0, scrollRect.vertical ? 1 - v : 0);
        }

        public void CloseList() => _model.CloseCallback?.Invoke();
    }
}