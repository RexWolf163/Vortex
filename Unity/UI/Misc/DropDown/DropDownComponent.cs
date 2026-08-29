using System;
using System.Collections.Generic;
using System.Linq;
using Sirenix.OdinInspector;
using Sirenix.Utilities;
using UnityEngine;
using UnityEngine.Events;
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

        [InfoBox("Компонент с кнопкой"), SerializeField]
        private UIComponent uiComponent;

        [SerializeField] private GameObject dropDownList;

        [InfoBox("Автоопределение направления по положению кнопки относительно центра экрана. Выкл. — всегда RightDown")]
        [SerializeField] private bool autoOrientation;

        [InfoBox("Точка открытия списка (RightDown). Обязательная — фолбек для остальных направлений")]
        [SerializeField]
        private Transform target;

        [ShowIf(nameof(autoOrientation)), SerializeField] private Transform targetRightTop;
        [ShowIf(nameof(autoOrientation)), SerializeField] private Transform targetLeftDown;
        [ShowIf(nameof(autoOrientation)), SerializeField] private Transform targetLeftTop;

        [SerializeField] [InfoBox("Может задаваться снаружи через метод SetList")]
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
            if (_sorted == null)
            {
                _sorted = dataList.ToArray();
                if (sorting)
                    _sorted.Sort();
            }

            if (_sorted.Length == 0)
                return;
            //_currentValue = _map[value];
            Select(_map[value]);
            if (_opened)
                UpdateList(ResolveDirection());
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
                _list.transform.localScale = Vector3.one;
            }

            // Позицию перевыставляем на КАЖДЫЙ открыв (а не только при создании): иначе список
            // остаётся на месте первого открытия, если кнопка сдвинулась (проскроллили контейнер).
            var dir = ResolveDirection();
            _list.transform.position = TargetFor(dir).position;
            UpdateList(dir);
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
            _currentValue = selectedIndex;
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

        private void UpdateList(DropDownDirection dir) =>
            DropDownList.Set(_sorted, Select, CloseList, _currentValue, closeOnSelected, scrollSensitivity, dir);

        /// <summary>
        /// Направление раскрытия. Авто выкл. — всегда RightDown (справа-вниз). Авто вкл. — по экранной
        /// позиции базовой точки <see cref="target"/> относительно центра экрана: кнопка левее центра →
        /// раскрытие вправо, выше центра → вниз (и наоборот), чтобы список уходил к центру, а не за край.
        /// </summary>
        private DropDownDirection ResolveDirection()
        {
            if (!autoOrientation)
                return DropDownDirection.RightDown;

            var canvas = GetComponentInParent<Canvas>();
            var cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera
                : null;
            var screen = RectTransformUtility.WorldToScreenPoint(cam, target.position);

            var right = screen.x < Screen.width * 0.5f;  // кнопка левее центра → раскрываем вправо
            var down = screen.y > Screen.height * 0.5f;  // кнопка выше центра → раскрываем вниз
            return (right, down) switch
            {
                (true, true) => DropDownDirection.RightDown,
                (false, true) => DropDownDirection.LeftDown,
                (true, false) => DropDownDirection.RightTop,
                (false, false) => DropDownDirection.LeftTop
            };
        }

        /// <summary>Точка привязки под направление; null-точка → фолбек на обязательную RightDown (<see cref="target"/>).</summary>
        private Transform TargetFor(DropDownDirection dir) => dir switch
        {
            DropDownDirection.RightTop => targetRightTop != null ? targetRightTop : target,
            DropDownDirection.LeftDown => targetLeftDown != null ? targetLeftDown : target,
            DropDownDirection.LeftTop => targetLeftTop != null ? targetLeftTop : target,
            _ => target
        };

        /// <summary>
        /// Включение-выключение компонента
        /// </summary>
        /// <param name="enable"></param>
        public void SetEnable(bool enable) =>
            uiComponent?.SetSwitcher(enable ? DropDownStates.Enabled : DropDownStates.Disabled);
    }
}