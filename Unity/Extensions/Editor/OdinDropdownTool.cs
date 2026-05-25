#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEngine;

namespace Vortex.Unity.Extensions.Editor
{
    /// <summary>
    /// Инструмент цельнотянут у коллеги (tg @RustyPhil)
    ///
    /// PS модифицирован
    /// </summary>
    public static class OdinDropdownTool
    {
        #region Dropdown

        public static T DropdownSelector<T>(T _value, IEnumerable<T> _items) => DropdownSelector(null, _value, _items);

        public static T DropdownSelector<T>(GUIContent _label, T _value, IEnumerable<T> _items)
        {
            var dropItems = new ValueDropdownList<T>();
            dropItems.AddRange(_items.Select(item => new ValueDropdownItem<T>(GetLabelDefault(item), item)));
            return DropdownSelector(_label, _value, dropItems, out var rect);
        }

        public static T DropdownSelector<T>(T _value, ValueDropdownList<T> _dropItems, out Rect rect) =>
            DropdownSelector(null, _value, _dropItems, out rect);

        public static T DropdownSelector<T>(GUIContent _label, T _value, ValueDropdownList<T> _dropItems,
            int rightOffSet = 0) =>
            DropdownSelector(_label, _value, _dropItems, out var rect, rightOffSet);

        public static T DropdownSelector<T>(GUIContent _label, T _value, ValueDropdownList<T> _dropItems,
            out Rect rect, int rightOffSet = 0)
        {
            rect = EditorGUILayout.GetControlRect(_label != null, EditorGUIUtility.singleLineHeight,
                EditorStyles.numberField);
            return DropdownSelector(rect, _label, _value, _dropItems, rightOffSet);
        }

        public static T DropdownSelector<T>(Rect _rect, GUIContent _label, T _value, IEnumerable<T> _items,
            int rightOffSet = 0)
        {
            var dropItems = new ValueDropdownList<T>();
            dropItems.AddRange(_items.Select(item => new ValueDropdownItem<T>(GetLabelDefault(item), item)));
            return DropdownSelector(_rect, _label, _value, dropItems, rightOffSet);
        }

        public static T DropdownSelector<T>(Rect _rect, GUIContent _label, T _value, ValueDropdownList<T> _dropItems,
            int rightOffSet = 0)
        {
            if (_label != null)
                _rect = EditorGUI.PrefixLabel(_rect, _label);

            if (rightOffSet > 0)
                _rect.xMax -= rightOffSet;

            var selected = _dropItems.Find(item => item.Value.Equals(_value));

            var result = OdinSelector<T>.DrawSelectorDropdown(_rect, selected.Text,
                (selectorRect) => ShowSelector(selectorRect, default, _dropItems, _value));

            if (result == null) return _value;

            using var enumerator = result.GetEnumerator();
            return enumerator.MoveNext() ? enumerator.Current : default;
        }

        private static OdinSelector<T> ShowSelector<T>(Rect _rect, Vector2 _dropdownSize, ValueDropdownList<T> _values,
            T _selection, int _minItemsForSerach = 10, string _dropdownTitle = null)
        {
            bool flag = _minItemsForSerach == 0 || _values != null &&
                _values.Take(_minItemsForSerach).Count() == _minItemsForSerach;
            var genericSelection = _values.Select(x => new GenericSelectorItem<T>(x.Text, x.Value));
            GenericSelector<T> selector = new GenericSelector<T>(_dropdownTitle, false, genericSelection);

            selector.SelectionTree.Config.DrawSearchToolbar = flag;
            selector.SetSelection(_selection);
            selector.FlattenedTree = true;
            selector.EnableSingleClickToSelect();

            selector.ShowInPopup(_rect, _dropdownSize);
            return selector;
        }

        private static string GetLabelDefault<T>(T _object)
        {
            return ((object)_object == null ? "Null" : _object.ToString());
        }

        private static IEnumerable<T> AddDefault<T>(IEnumerable<T> _items)
        {
            yield return default;
            foreach (T item in _items)
                yield return item;
        }

        #endregion
    }
}
#endif