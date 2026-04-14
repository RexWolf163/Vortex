#if UNITY_EDITOR && ODIN_INSPECTOR
using System;
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
        private bool _foldout;
        private object _cachedValue;
        private List<PropertyEntry> _properties;
        private List<MethodEntry> _methods;

        private struct PropertyEntry
        {
            public PropertyInfo Info;
            public string Label;
            public bool CanWrite;
            public bool IsReactive;
        }

        private struct MethodEntry
        {
            public MethodInfo Info;
            public string Label;
        }

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

            if (value != _cachedValue)
            {
                _cachedValue = value;
                RebuildEntries(value);
            }

            _foldout = EditorGUILayout.Foldout(_foldout, label?.text ?? Property.Name, true, EditorStyles.foldoutHeader);
            if (!_foldout) return;

            EditorGUI.indentLevel++;
            DrawProperties(value);
            DrawMethods(value);
            EditorGUI.indentLevel--;
        }

        private void RebuildEntries(object target)
        {
            _properties = new List<PropertyEntry>();
            _methods = new List<MethodEntry>();

            var type = target.GetType();
            const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance;

            foreach (var prop in type.GetProperties(flags))
            {
                if (!prop.CanRead) continue;
                if (prop.GetIndexParameters().Length > 0) continue;

                _properties.Add(new PropertyEntry
                {
                    Info = prop,
                    Label = prop.Name,
                    CanWrite = prop.CanWrite,
                    IsReactive = typeof(IReactiveData).IsAssignableFrom(prop.PropertyType)
                });
            }

            foreach (var method in type.GetMethods(flags | BindingFlags.NonPublic))
            {
                var attr = method.GetCustomAttribute<DataModelMethodAttribute>();
                if (attr == null) continue;

                _methods.Add(new MethodEntry
                {
                    Info = method,
                    Label = string.IsNullOrEmpty(attr.Label) ? method.Name : attr.Label
                });
            }
        }

        private void DrawProperties(object target)
        {
            foreach (var entry in _properties)
            {
                try
                {
                    var value = entry.Info.GetValue(target);

                    if (entry.IsReactive)
                    {
                        DrawReactiveValue(entry, value);
                        continue;
                    }

                    if (entry.CanWrite)
                        DrawEditableProperty(entry, target, value);
                    else
                        DrawReadOnlyProperty(entry, value);
                }
                catch (Exception e)
                {
                    EditorGUILayout.LabelField(entry.Label, $"[Error: {e.Message}]");
                }
            }
        }

        private void DrawReactiveValue(PropertyEntry entry, object reactive)
        {
            if (reactive == null)
            {
                EditorGUILayout.LabelField(entry.Label, "[NULL]");
                return;
            }

            switch (reactive)
            {
                case IntData intData:
                {
                    EditorGUI.BeginChangeCheck();
                    var newVal = EditorGUILayout.IntField(entry.Label, intData.Value);
                    if (EditorGUI.EndChangeCheck())
                        intData.Set(newVal);
                    break;
                }
                case FloatData floatData:
                {
                    EditorGUI.BeginChangeCheck();
                    var newVal = EditorGUILayout.FloatField(entry.Label, floatData.Value);
                    if (EditorGUI.EndChangeCheck())
                        floatData.Set(newVal);
                    break;
                }
                case BoolData boolData:
                {
                    EditorGUI.BeginChangeCheck();
                    var newVal = EditorGUILayout.Toggle(entry.Label, boolData.Value);
                    if (EditorGUI.EndChangeCheck())
                        boolData.Set(newVal);
                    break;
                }
                case StringData stringData:
                {
                    EditorGUI.BeginChangeCheck();
                    var newVal = EditorGUILayout.TextField(entry.Label, stringData.Value);
                    if (EditorGUI.EndChangeCheck())
                        stringData.Set(newVal);
                    break;
                }
                default:
                    EditorGUILayout.LabelField(entry.Label, reactive.ToString());
                    break;
            }
        }

        private void DrawEditableProperty(PropertyEntry entry, object target, object value)
        {
            var type = entry.Info.PropertyType;

            EditorGUI.BeginChangeCheck();
            object newValue = value;

            if (type == typeof(int))
                newValue = EditorGUILayout.IntField(entry.Label, (int)(value ?? 0));
            else if (type == typeof(float))
                newValue = EditorGUILayout.FloatField(entry.Label, (float)(value ?? 0f));
            else if (type == typeof(bool))
                newValue = EditorGUILayout.Toggle(entry.Label, (bool)(value ?? false));
            else if (type == typeof(string))
                newValue = EditorGUILayout.TextField(entry.Label, (string)value ?? "");
            else if (type == typeof(long))
                newValue = EditorGUILayout.LongField(entry.Label, (long)(value ?? 0L));
            else if (type == typeof(double))
                newValue = EditorGUILayout.DoubleField(entry.Label, (double)(value ?? 0.0));
            else if (type == typeof(Vector2))
                newValue = EditorGUILayout.Vector2Field(entry.Label, (Vector2)(value ?? Vector2.zero));
            else if (type == typeof(Vector3))
                newValue = EditorGUILayout.Vector3Field(entry.Label, (Vector3)(value ?? Vector3.zero));
            else if (type == typeof(Color))
                newValue = EditorGUILayout.ColorField(entry.Label, (Color)(value ?? Color.white));
            else if (type.IsEnum)
                newValue = EditorGUILayout.EnumPopup(entry.Label, (Enum)(value ?? Enum.ToObject(type, 0)));
            else
            {
                EditorGUILayout.LabelField(entry.Label, value?.ToString() ?? "[NULL]");
                return;
            }

            if (EditorGUI.EndChangeCheck())
            {
                try
                {
                    entry.Info.SetValue(target, newValue);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[DataModel] Failed to set {entry.Label}: {e.Message}");
                }
            }
        }

        private static void DrawReadOnlyProperty(PropertyEntry entry, object value)
        {
            EditorGUI.BeginDisabledGroup(true);

            var type = entry.Info.PropertyType;

            if (type == typeof(int))
                EditorGUILayout.IntField(entry.Label, (int)(value ?? 0));
            else if (type == typeof(float))
                EditorGUILayout.FloatField(entry.Label, (float)(value ?? 0f));
            else if (type == typeof(bool))
                EditorGUILayout.Toggle(entry.Label, (bool)(value ?? false));
            else if (type == typeof(string))
                EditorGUILayout.TextField(entry.Label, (string)value ?? "");
            else if (type == typeof(long))
                EditorGUILayout.LongField(entry.Label, (long)(value ?? 0L));
            else if (type == typeof(double))
                EditorGUILayout.DoubleField(entry.Label, (double)(value ?? 0.0));
            else if (type == typeof(Vector2))
                EditorGUILayout.Vector2Field(entry.Label, (Vector2)(value ?? Vector2.zero));
            else if (type == typeof(Vector3))
                EditorGUILayout.Vector3Field(entry.Label, (Vector3)(value ?? Vector3.zero));
            else if (type == typeof(Color))
                EditorGUILayout.ColorField(entry.Label, (Color)(value ?? Color.white));
            else if (type.IsEnum)
                EditorGUILayout.EnumPopup(entry.Label, (Enum)(value ?? Enum.ToObject(type, 0)));
            else
                EditorGUILayout.LabelField(entry.Label, value?.ToString() ?? "[NULL]");

            EditorGUI.EndDisabledGroup();
        }

        private void DrawMethods(object target)
        {
            if (_methods.Count == 0) return;

            EditorGUILayout.Space(4);
            foreach (var method in _methods)
            {
                if (GUILayout.Button(method.Label))
                {
                    try
                    {
                        method.Info.Invoke(target, null);
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning($"[DataModel] {method.Label}: {e.Message}");
                    }
                }
            }
        }
    }
}
#endif
