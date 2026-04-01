#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Vortex.Unity.EditorTools.Elements;
using Object = UnityEngine.Object;

namespace Vortex.Unity.EditorTools.Abstraction
{
    public class PropertyData
    {
        public Rect Position;
        public SerializedProperty Property { get; private set; }
        public object Owner { get; private set; }
        public FieldInfo FieldInfo { get; private set; }
        public GUIContent Label { get; internal set; }
        public bool IsLabelVisible { get; private set; } = true;
        public bool IsFieldVisible { get; private set; } = true;
        public bool IsFieldDefault { get; private set; } = true;
        public bool IsLabelDefault { get; private set; } = true;

        public float BaseHeight { get; set; } = 0f;

        [Obsolete] public (PropertyAttribute attribute, IMultiDrawerAttribute drawer)? LabelDrawer { get; private set; }

        public void HideLabel() => IsLabelVisible = false;
        public void HideField() => IsFieldVisible = false;
        public void IsCustomLabel() => IsLabelDefault = false;
        public void IsCustomField() => IsFieldDefault = false;

        public float Height { get; private set; }

        public float Width { get; internal set; }

        public MethodInfo Method { get; internal set; }

        private Dictionary<Type, object> _drawerCache;

        public T GetDrawerCache<T>(Type key) where T : class
        {
            if (_drawerCache != null && _drawerCache.TryGetValue(key, out var val))
                return val as T;
            return null;
        }

        public void SetDrawerCache(Type key, object value)
        {
            _drawerCache ??= new Dictionary<Type, object>();
            _drawerCache[key] = value;
        }

        public void AddHeight(float height) => Height += height;

        public void Update(Rect position, GUIContent label)
        {
            Position = position;
            Label = label;
        }

        /// <summary>
        /// Сброс состояния для метода
        /// </summary>
        /// <param name="owner"></param>
        /// <param name="method"></param>
        public void Reset(Object owner, MethodInfo method)
        {
            Position = Rect.zero;
            Label = null;
            Property = null;
            FieldInfo = null;
            IsLabelVisible = true;
            IsFieldVisible = true;
            IsFieldDefault = true;
            IsLabelDefault = true;
            Method = method;
            Height = 0;
            Owner = owner;
        }

        /// <summary>
        /// Очистка всех кастомных параметров
        /// (кроме тех что задаются при Reset)
        /// </summary>
        public void Reset()
        {
            if (Method != null)
                Reset((Object)Owner, Method);
            else
                Reset(Label, Property, FieldInfo);
        }

        /// <summary>
        /// Сброс состояния для переиспользования без аллокации.
        /// </summary>
        internal void Reset(GUIContent label, SerializedProperty property, FieldInfo fieldInfo)
        {
            Position = Rect.zero;
            Label = label;
            Property = property;
            FieldInfo = fieldInfo;
            IsLabelVisible = true;
            IsFieldVisible = true;
            IsFieldDefault = true;
            IsLabelDefault = true;
            Method = null;
            Height = 0;
            Owner = ReflectionHelper.ResolveOwner(property);
        }
    }
}
#endif