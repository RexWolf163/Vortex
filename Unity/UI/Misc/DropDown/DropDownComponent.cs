using System;
using System.Collections.Generic;
using System.Linq;
using Sirenix.Utilities;
using UnityEngine;
using UnityEngine.Events;
using Vortex.Unity.EditorTools.Attributes;
using Vortex.Unity.UI.UIComponents;

namespace Vortex.Unity.UI.Misc.DropDown
{
    /// <summary>
    /// Компонент выпадашки
    /// </summary>
    public class DropDownComponent : MonoBehaviour
    {
        private enum DropDownStates
        {
            Disabled,
            Enabled,
            Opened,
        }

        [InfoBubble("Компонент с кнопкой"), SerializeField]
        private UIComponent uiComponent;

        [SerializeField] private GameObject dropDownList;

        [InfoBubble("Точка открытия списка")] [SerializeField]
        private Transform target;

        [SerializeField] [InfoBubble("Может задаваться снаружи через метод SetList")]
        private string[] dataList;

        [SerializeField] private UnityEvent<int> onSelected;

        [SerializeField] private bool closeOnSelected;
        [SerializeField] private bool sorting;

        [SerializeField] private int scrollSensitivity = 1;

        private Transform _parent;

        private Action<int> _callback;
        private GameObject _list;
        private DropDownList _dropDownList;
        private DropDownList DropDownList => _dropDownList ??= _list?.GetComponent<DropDownList>();

        private bool _opened;
        private bool _wasInit;

        /// <summary>
        /// Индекс отсортированного порядка
        /// </summary>
        private int _currentValue;

        /// <summary>
        /// Сортированный список
        /// </summary>
        private string[] _sorted;

        /// <summary>
        /// Карта соответствий.
        /// Первое число - номер исходного списка - второе номер сортированного
        /// </summary>
        private readonly Dictionary<int, int> _map = new();

        /// <summary>
        /// Обратная карта соответствий.
        /// Первое число - номер сортированного списка - второе номер исходного
        /// </summary>
        private readonly Dictionary<int, int> _mapBack = new();

        /// <summary>
        /// Задать параметры списка
        /// </summary>
        /// <param name="text"></param>
        /// <param name="callback"></param>
        /// <param name="value"></param>
        public void SetList(IReadOnlyList<string> text, Action<int> callback, int value = 0)
        {
            dataList = text.ToArray();
            if (dataList == null || dataList.Length == 0)
                return;
            _wasInit = true;
            var c = dataList?.Length ?? 0;
            _sorted = dataList.ToArray();
            if (sorting)
                _sorted.Sort();

            _map.Clear();
            _mapBack.Clear();
            for (var i = 0; i < c; i++)
            {
                var sortI = Array.IndexOf(_sorted, dataList[i]);
                _map[i] = sortI;
                _mapBack[sortI] = i;
            }

            _callback = callback;
            _currentValue = _map[value];
            uiComponent?.SetText(c == 0 ? "" : c > value ? _sorted[_map[value]] : _sorted[0]);
            uiComponent?.SetSwitcher(DropDownStates.Enabled);
            if (_opened)
                OpenList();
        }

        /// <summary>
        /// Установить новое значение 
        /// </summary>
        /// <param name="value"></param>
        public void SetValue(int value)
        {
            if (_sorted.Length == 0)
                return;
            _currentValue = _map[value];
            Select(_currentValue);
            if (_opened)
                DropDownList.Set(_sorted, Select, CloseList, _currentValue, closeOnSelected);
        }

        /// <summary>
        /// Получить текущее значение
        /// </summary>
        /// <returns></returns>
        public int GetValue()
        {
            if (dataList.Length == 0)
                return -1;
            return _mapBack[_currentValue];
        }

        /// <summary>
        /// Получить текущее значение
        /// </summary>
        /// <returns></returns>
        public string GetValueItem()
        {
            return _sorted.Length == 0 ? null : _sorted[_currentValue];
        }

        private void OnEnable()
        {
            uiComponent?.SetAction(ToggleList);
        }

        private void OnDisable()
        {
            uiComponent?.SetAction(null);
        }

        private void OnDestroy()
        {
            Destroy(_list);
        }

        private void ToggleList()
        {
            if (_opened)
                CloseList();
            else
                OpenList();
        }

        private void OpenList()
        {
            if (!_wasInit)
                SetList(dataList, null);
            _opened = true;
            if (_list == null)
            {
                _parent ??= GetComponentInParent<Canvas>().transform;
                _list = Instantiate(dropDownList, _parent.transform, true);
                _list.transform.position = target.position;
                _list.transform.localScale = Vector3.one;
            }

            UpdateList();
            _list.SetActive(true);
            uiComponent?.SetSwitcher(DropDownStates.Opened);
        }

        private void CloseList()
        {
            _opened = false;
            _list?.SetActive(false);
            uiComponent?.SetSwitcher(DropDownStates.Enabled);
        }

        /// <summary>
        /// Выставление номера по сортированному списку
        /// </summary>
        /// <param name="selectedIndex"></param>
        private void Select(int selectedIndex)
        {
            var index = _mapBack[selectedIndex];
            _currentValue = index;
            _callback?.Invoke(index);
            onSelected?.Invoke(index);
            var c = _sorted.Length;
            uiComponent?.SetText(c == 0 ? "" : c > selectedIndex ? _sorted[selectedIndex] : _sorted[0]);
        }

        private void Awake()
        {
            if (_wasInit || dataList.Length == 0)
                return;
            SetList(dataList, null);
        }

        private void UpdateList() =>
            DropDownList.Set(_sorted, Select, CloseList, _currentValue, closeOnSelected, scrollSensitivity);

        /// <summary>
        /// Включение-выключение компонента
        /// </summary>
        /// <param name="enable"></param>
        public void SetEnable(bool enable) =>
            uiComponent?.SetSwitcher(enable ? DropDownStates.Enabled : DropDownStates.Disabled);
    }
}