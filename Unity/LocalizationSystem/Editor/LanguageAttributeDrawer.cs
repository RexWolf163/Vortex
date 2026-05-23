#if UNITY_EDITOR && ODIN_INSPECTOR
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEngine;
using Vortex.Core.Extensions.LogicExtensions;
using Vortex.Core.LocalizationSystem.Bus;
using Vortex.Unity.EditorTools.Elements;

namespace Vortex.Unity.LocalizationSystem.Editor
{
    /// <summary>
    /// Odin-drawer для <see cref="LanguageAttribute"/>.
    /// Заменяет string-значение на SearchablePopup со списком зарегистрированных языков.
    /// Работает для [SerializeField], [ShowInInspector] и параметров методов —
    /// чтение и запись идут через <c>ValueEntry.SmartValue</c>, без зависимости от SerializedProperty.
    /// </summary>
    public sealed class LanguageAttributeDrawer : OdinAttributeDrawer<LanguageAttribute, string>
    {
        protected override void DrawPropertyLayout(GUIContent label)
        {
            var rect = EditorGUILayout.GetControlRect();
            if (label != null)
                rect = EditorGUI.PrefixLabel(rect, label);

            var list = Localization.GetLanguages();
            var keys = list.ToArray();
            var current = ValueEntry.SmartValue;
            var currentIndex = current.IsNullOrWhitespace() ? -1 : list.IndexOf(current);

            DrawingUtility.DrawSelector(
                rect,
                current,
                keys,
                newValue => ValueEntry.SmartValue = newValue,
                currentIndex);
        }
    }
}
#endif
