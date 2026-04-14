#if UNITY_EDITOR && ODIN_INSPECTOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEngine;
using Vortex.Core.Extensions.ReactiveValues;

namespace Vortex.Unity.EditorTools.DataModelSystem
{
    public class DataModelDrawer : OdinAttributeDrawer<DataModelAttribute>
    {
        private const int MaxDepth = 5;

        private bool _foldout;

        protected override void DrawPropertyLayout(GUIContent label)
        {
            if (!Application.isPlaying)
            {
                CallNextDrawer(label);
                return;
            }

            var value = Property.ValueEntry?.WeakSmartValue;
            if (value == null)
            {
                EditorGUILayout.LabelField(label?.text ?? Property.Name, "[NULL]");
                return;
            }

            _foldout = EditorGUILayout.Foldout(_foldout, label?.text ?? Property.Name, true, EditorStyles.foldoutHeader);
            if (!_foldout) return;

            EditorGUI.indentLevel++;
            DrawObject(value, 0);
            EditorGUI.indentLevel--;
        }

        #region Object

        private static void DrawObject(object target, int depth)
        {
            if (target == null || depth > MaxDepth) return;

            // Сам объект — коллекция? Рисуем как коллекцию, не как набор свойств
            if (target is IDictionary dict) { DrawDictionaryItems(dict, depth); return; }
            if (target is IList list) { DrawListItems(list, depth); return; }

            var type = target.GetType();
            const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance;

            foreach (var prop in type.GetProperties(flags))
            {
                if (!prop.CanRead || prop.GetIndexParameters().Length > 0) continue;

                try
                {
                    var value = prop.GetValue(target);
                    DrawValue(prop.Name, prop.PropertyType, value, prop.CanWrite ? target : null, prop, depth);
                }
                catch (Exception e)
                {
                    EditorGUILayout.LabelField(prop.Name, $"[Error: {e.Message}]");
                }
            }

            // Методы с [DataModelMethod]
            foreach (var method in type.GetMethods(flags | BindingFlags.NonPublic))
            {
                var attr = method.GetCustomAttribute<DataModelMethodAttribute>();
                if (attr == null) continue;
                var btnLabel = string.IsNullOrEmpty(attr.Label) ? method.Name : attr.Label;
                if (GUILayout.Button(btnLabel))
                {
                    try { method.Invoke(target, null); }
                    catch (Exception e) { Debug.LogWarning($"[DataModel] {btnLabel}: {e.Message}"); }
                }
            }
        }

        #endregion

        #region Value

        private static void DrawValue(string label, Type type, object value, object writeTarget, PropertyInfo writeProp, int depth)
        {
            // NULL
            if (value == null)
            {
                EditorGUILayout.LabelField(label, "[NULL]");
                return;
            }

            // ReactiveValue
            if (value is IReactiveData)
            {
                DrawReactiveValue(label, value);
                return;
            }

            // Примитивы и known-типы
            if (TryDrawPrimitive(label, type, value, writeTarget, writeProp))
                return;

            // Коллекции
            if (value is IDictionary dict)
            {
                DrawDictionary(label, dict, depth);
                return;
            }

            if (value is IList list)
            {
                DrawList(label, list, depth);
                return;
            }

            if (value is IEnumerable enumerable && type != typeof(string))
            {
                DrawEnumerable(label, enumerable, depth);
                return;
            }

            // Сложный объект — рекурсия
            DrawComplexObject(label, value, depth);
        }

        #endregion

        #region Primitives

        private static bool TryDrawPrimitive(string label, Type type, object value, object writeTarget, PropertyInfo writeProp)
        {
            var canWrite = writeTarget != null && writeProp != null;

            if (type == typeof(int))
                return DrawPrimitiveField(label, (int)value, canWrite, v => EditorGUILayout.IntField(label, v), writeTarget, writeProp);
            if (type == typeof(float))
                return DrawPrimitiveField(label, (float)value, canWrite, v => EditorGUILayout.FloatField(label, v), writeTarget, writeProp);
            if (type == typeof(bool))
                return DrawPrimitiveField(label, (bool)value, canWrite, v => EditorGUILayout.Toggle(label, v), writeTarget, writeProp);
            if (type == typeof(string))
                return DrawPrimitiveField(label, (string)value ?? "", canWrite, v => EditorGUILayout.TextField(label, v), writeTarget, writeProp);
            if (type == typeof(long))
                return DrawPrimitiveField(label, (long)value, canWrite, v => EditorGUILayout.LongField(label, v), writeTarget, writeProp);
            if (type == typeof(double))
                return DrawPrimitiveField(label, (double)value, canWrite, v => EditorGUILayout.DoubleField(label, v), writeTarget, writeProp);
            if (type == typeof(Vector2))
                return DrawPrimitiveField(label, (Vector2)value, canWrite, v => EditorGUILayout.Vector2Field(label, v), writeTarget, writeProp);
            if (type == typeof(Vector3))
                return DrawPrimitiveField(label, (Vector3)value, canWrite, v => EditorGUILayout.Vector3Field(label, v), writeTarget, writeProp);
            if (type == typeof(Color))
                return DrawPrimitiveField(label, (Color)value, canWrite, v => EditorGUILayout.ColorField(label, v), writeTarget, writeProp);
            if (type.IsEnum)
                return DrawPrimitiveField(label, (Enum)value, canWrite, v => EditorGUILayout.EnumPopup(label, v), writeTarget, writeProp);

            return false;
        }

        private static bool DrawPrimitiveField<T>(string label, T value, bool canWrite,
            Func<T, T> drawFunc, object writeTarget, PropertyInfo writeProp)
        {
            if (!canWrite)
            {
                EditorGUI.BeginDisabledGroup(true);
                drawFunc(value);
                EditorGUI.EndDisabledGroup();
                return true;
            }

            EditorGUI.BeginChangeCheck();
            var newValue = drawFunc(value);
            if (EditorGUI.EndChangeCheck())
            {
                try { writeProp.SetValue(writeTarget, newValue); }
                catch (Exception e) { Debug.LogWarning($"[DataModel] Failed to set {label}: {e.Message}"); }
            }

            return true;
        }

        #endregion

        #region Reactive

        private static void DrawReactiveValue(string label, object reactive)
        {
            switch (reactive)
            {
                case IntData intData:
                    EditorGUI.BeginChangeCheck();
                    var intVal = EditorGUILayout.IntField(label, intData.Value);
                    if (EditorGUI.EndChangeCheck()) intData.Set(intVal);
                    break;
                case FloatData floatData:
                    EditorGUI.BeginChangeCheck();
                    var floatVal = EditorGUILayout.FloatField(label, floatData.Value);
                    if (EditorGUI.EndChangeCheck()) floatData.Set(floatVal);
                    break;
                case BoolData boolData:
                    EditorGUI.BeginChangeCheck();
                    var boolVal = EditorGUILayout.Toggle(label, boolData.Value);
                    if (EditorGUI.EndChangeCheck()) boolData.Set(boolVal);
                    break;
                case StringData stringData:
                    EditorGUI.BeginChangeCheck();
                    var strVal = EditorGUILayout.TextField(label, stringData.Value);
                    if (EditorGUI.EndChangeCheck()) stringData.Set(strVal);
                    break;
                default:
                    EditorGUILayout.LabelField(label, reactive.ToString());
                    break;
            }
        }

        #endregion

        #region Collections

        private static readonly Dictionary<int, bool> CollectionFoldouts = new();

        private static bool GetFoldout(string label, object collection)
        {
            var key = (label + collection.GetHashCode()).GetHashCode();
            CollectionFoldouts.TryGetValue(key, out var state);
            return state;
        }

        private static void SetFoldout(string label, object collection, bool state)
        {
            var key = (label + collection.GetHashCode()).GetHashCode();
            CollectionFoldouts[key] = state;
        }

        private static void DrawList(string label, IList list, int depth)
        {
            var foldout = GetFoldout(label, list);
            foldout = EditorGUILayout.Foldout(foldout, $"{label} [{list.Count}]", true);
            SetFoldout(label, list, foldout);
            if (!foldout) return;

            EditorGUI.indentLevel++;
            DrawListItems(list, depth);
            EditorGUI.indentLevel--;
        }

        private static void DrawListItems(IList list, int depth)
        {
            for (var i = 0; i < list.Count; i++)
            {
                var item = list[i];
                if (item == null)
                {
                    EditorGUILayout.LabelField($"[{i}]", "[NULL]");
                    continue;
                }

                DrawValue($"[{i}]", item.GetType(), item, null, null, depth + 1);
            }
        }

        private static void DrawDictionary(string label, IDictionary dict, int depth)
        {
            var foldout = GetFoldout(label, dict);
            foldout = EditorGUILayout.Foldout(foldout, $"{label} [{dict.Count}]", true);
            SetFoldout(label, dict, foldout);
            if (!foldout) return;

            EditorGUI.indentLevel++;
            DrawDictionaryItems(dict, depth);
            EditorGUI.indentLevel--;
        }

        private static void DrawDictionaryItems(IDictionary dict, int depth)
        {
            foreach (DictionaryEntry entry in dict)
            {
                var key = entry.Key?.ToString() ?? "[NULL]";
                var val = entry.Value;
                if (val == null)
                {
                    EditorGUILayout.LabelField(key, "[NULL]");
                    continue;
                }

                DrawValue(key, val.GetType(), val, null, null, depth + 1);
            }
        }

        private static void DrawEnumerable(string label, IEnumerable enumerable, int depth)
        {
            var items = new List<object>();
            foreach (var item in enumerable)
                items.Add(item);

            var foldout = GetFoldout(label, enumerable);
            foldout = EditorGUILayout.Foldout(foldout, $"{label} [{items.Count}]", true);
            SetFoldout(label, enumerable, foldout);
            if (!foldout) return;

            EditorGUI.indentLevel++;
            for (var i = 0; i < items.Count; i++)
            {
                var item = items[i];
                if (item == null)
                {
                    EditorGUILayout.LabelField($"[{i}]", "[NULL]");
                    continue;
                }

                DrawValue($"[{i}]", item.GetType(), item, null, null, depth + 1);
            }
            EditorGUI.indentLevel--;
        }

        #endregion

        #region Complex

        private static void DrawComplexObject(string label, object value, int depth)
        {
            if (depth >= MaxDepth)
            {
                EditorGUILayout.LabelField(label, value.ToString());
                return;
            }

            var foldout = GetFoldout(label, value);
            foldout = EditorGUILayout.Foldout(foldout, $"{label} ({value.GetType().Name})", true);
            SetFoldout(label, value, foldout);
            if (!foldout) return;

            EditorGUI.indentLevel++;
            DrawObject(value, depth + 1);
            EditorGUI.indentLevel--;
        }

        #endregion
    }
}
#endif
