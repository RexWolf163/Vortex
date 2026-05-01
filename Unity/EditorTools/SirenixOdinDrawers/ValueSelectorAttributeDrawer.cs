using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Sirenix.OdinInspector.Editor;
using Sirenix.OdinInspector.Editor.ValueResolvers;
using Sirenix.Utilities.Editor;
using UnityEditor;
using UnityEngine;
using Vortex.Unity.EditorTools.Attributes;
using Vortex.Unity.EditorTools.Elements;

namespace Vortex.Unity.EditorTools.SirenixOdinDrawers
{
    /// <summary>
    /// Odin-drawer для <see cref="ValueSelectorAttribute"/>.
    /// Заменяет поле на SearchablePopup с вариантами из метода:
    /// • string[] / List&lt;string&gt; / IEnumerable&lt;string&gt; — ключ = значение
    /// • Dictionary&lt;string, TValue&gt; — ключи в popup, TValue пишется в поле
    /// </summary>
    public sealed class ValueSelectorAttributeDrawer : OdinAttributeDrawer<ValueSelectorAttribute>
    {
        private ValueResolver<object> _resolver;
        private string _staticError;

        protected override void Initialize()
        {
            _resolver = ValueResolver.Get<object>(Property, Attribute.MethodName);
        }

        protected override void DrawPropertyLayout(GUIContent label)
        {
            if (_resolver.HasError)
            {
                _resolver.DrawError();
                CallNextDrawer(label);
                return;
            }

            var raw = _resolver.GetValue();
            if (raw == null)
            {
                SirenixEditorGUI.ErrorMessageBox($"[ValueSelector] '{Attribute.MethodName}' returned null.");
                CallNextDrawer(label);
                return;
            }

            if (!Extract(raw, out var keys, out var values, out var isDictionary))
            {
                SirenixEditorGUI.ErrorMessageBox(
                    $"[ValueSelector] Unsupported return type: {raw.GetType().Name}.\n" +
                    "Expected: string[], List<string>, IEnumerable<string>, or Dictionary<string, T>.");
                CallNextDrawer(label);
                return;
            }

            if (keys.Length == 0)
            {
                SirenixEditorGUI.ErrorMessageBox($"'{Attribute.MethodName}' returned empty collection.");
                CallNextDrawer(label);
                return;
            }

            var unityProp = Property.Tree.UnitySerializedObject?.FindProperty(Property.UnityPropertyPath);
            if (unityProp == null)
            {
                CallNextDrawer(label);
                return;
            }

            var rect = EditorGUILayout.GetControlRect();
            if (label != null)
                rect = EditorGUI.PrefixLabel(rect, label);

            var currentIndex = FindCurrentIndex(unityProp, keys, values, isDictionary);
            DrawingUtility.DrawSelector(rect, unityProp, keys, values, currentIndex, Attribute.Placeholder);
        }

        // ════════════════════════════════════════════════════════
        //  Извлечение коллекции
        // ════════════════════════════════════════════════════════

        private static bool Extract(object result, out string[] keys, out object[] values, out bool isDictionary)
        {
            keys = null;
            values = null;
            isDictionary = false;

            if (result is string[] sa)
            {
                keys = sa;
                return true;
            }

            if (result is List<string> sl)
            {
                keys = sl.ToArray();
                return true;
            }

            var rt = result.GetType();
            if (!IsDictType(rt) && result is IEnumerable<string> se)
            {
                keys = se.ToArray();
                return true;
            }

            if (IsDictType(rt) && result is IDictionary dict)
            {
                var kl = new List<string>();
                var vl = new List<object>();
                foreach (DictionaryEntry e in dict)
                {
                    kl.Add(e.Key?.ToString() ?? "");
                    vl.Add(e.Value);
                }

                keys = kl.ToArray();
                values = vl.ToArray();
                isDictionary = true;
                return true;
            }

            return false;
        }

        private static bool IsDictType(Type t) =>
            t.IsGenericType && t.GetGenericTypeDefinition() == typeof(Dictionary<,>);

        private static int FindCurrentIndex(SerializedProperty property, string[] keys, object[] values,
            bool isDictionary)
        {
            if (keys == null) return -1;

            if (property.propertyType == SerializedPropertyType.String)
            {
                var val = property.stringValue;
                if (string.IsNullOrEmpty(val)) return -1;

                if (isDictionary && values != null)
                {
                    for (int i = 0; i < values.Length; i++)
                        if (val.Equals(values[i]?.ToString()))
                            return i;
                    return -1;
                }

                return Array.IndexOf(keys, val);
            }

            if (!isDictionary || values == null) return -1;

            var fieldValue = ReadFieldValue(property);
            if (fieldValue == null) return -1;

            for (int i = 0; i < values.Length; i++)
                if (values[i] != null && values[i].Equals(fieldValue))
                    return i;

            return -1;
        }

        private static object ReadFieldValue(SerializedProperty property)
        {
            switch (property.propertyType)
            {
                case SerializedPropertyType.String: return property.stringValue;
                case SerializedPropertyType.Integer: return property.intValue;
                case SerializedPropertyType.Float: return property.floatValue;
                case SerializedPropertyType.Boolean: return property.boolValue;
                case SerializedPropertyType.Enum: return property.enumValueIndex;
                case SerializedPropertyType.ObjectReference: return property.objectReferenceValue;
                default: return null;
            }
        }
    }
}
