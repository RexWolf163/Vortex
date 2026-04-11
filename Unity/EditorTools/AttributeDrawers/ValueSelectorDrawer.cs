#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Vortex.Unity.EditorTools.Abstraction;
using Vortex.Unity.EditorTools.Attributes;
using Vortex.Unity.EditorTools.Elements;
using Object = UnityEngine.Object;

namespace Vortex.Unity.EditorTools.AttributeDrawers
{
    [CustomPropertyDrawer(typeof(ValueSelectorAttribute))]
    public class ValueSelectorDrawer : MultiDrawer
    {
        internal class SelectorCache
        {
            public string[] Keys;
            public object[] Values;
            public bool IsDictionary;
            public int Hash;
            public string Error;
            public bool HasError;
            public double Timestamp;
        }

        private static SelectorCache Resolve(PropertyData data, ValueSelectorAttribute attr)
        {
            var property = data.Property;
            var methodName = attr.MethodName;
            var hash = property.serializedObject.targetObject.GetInstanceID()
                       ^ methodName.GetHashCode()
                       ^ property.propertyPath.GetHashCode();

            var cached = data.GetDrawerCache<SelectorCache>(typeof(ValueSelectorAttribute));
            if (cached != null && cached.Keys != null && cached.Hash == hash
                && EditorApplication.timeSinceStartup - cached.Timestamp < 1.0)
                return cached;

            var entry = new SelectorCache();

            var owner = ReflectionHelper.ResolveOwner(property);
            var ownerObject = owner as Object;
            var method = ReflectionHelper.FindMethod(owner, methodName);
            if (method == null && ownerObject != null && ownerObject != property.serializedObject.targetObject)
            {
                owner = property.serializedObject.targetObject;
                method = ReflectionHelper.FindMethod(owner, methodName);
            }

            if (method == null)
            {
                entry.HasError = true;
                entry.Error = $"[ValueSelector] Method '{methodName}' not found on {owner.GetType().Name}.";
                data.SetDrawerCache(typeof(ValueSelectorAttribute), entry);
                return entry;
            }

            object result;
            try
            {
                result = method.Invoke(method.IsStatic ? null : owner, null);
            }
            catch (Exception e)
            {
                entry.HasError = true;
                entry.Error = $"[ValueSelector] '{methodName}' threw: {e.InnerException?.Message ?? e.Message}";
                data.SetDrawerCache(typeof(ValueSelectorAttribute), entry);
                return entry;
            }

            if (result == null)
            {
                entry.HasError = true;
                entry.Error = $"[ValueSelector] '{methodName}' returned null.";
                data.SetDrawerCache(typeof(ValueSelectorAttribute), entry);
                return entry;
            }

            if (!Extract(result, out entry.Keys, out entry.Values, out entry.IsDictionary))
            {
                entry.HasError = true;
                entry.Error = $"[ValueSelector] Unsupported return type: {result.GetType().Name}.\n" +
                              "Expected: string[], List<string>, or Dictionary<string, T>.";
                data.SetDrawerCache(typeof(ValueSelectorAttribute), entry);
                return entry;
            }

            if (entry.Keys.Length == 0)
            {
                entry.HasError = true;
                entry.Error = $"'{methodName}' returned empty collection.";
                data.SetDrawerCache(typeof(ValueSelectorAttribute), entry);
                return entry;
            }

            entry.Hash = hash;
            entry.Timestamp = EditorApplication.timeSinceStartup;
            data.SetDrawerCache(typeof(ValueSelectorAttribute), entry);
            return entry;
        }

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

        private static int FindCurrentIndex(SerializedProperty property, SelectorCache entry)
        {
            if (entry.Keys == null)
                return -1;

            if (property.propertyType == SerializedPropertyType.String)
            {
                var val = property.stringValue;
                if (string.IsNullOrEmpty(val)) return -1;

                if (entry.IsDictionary && entry.Values != null)
                {
                    for (int i = 0; i < entry.Values.Length; i++)
                    {
                        if (val.Equals(entry.Values[i]?.ToString()))
                            return i;
                    }
                    return -1;
                }

                return Array.IndexOf(entry.Keys, val);
            }

            if (!entry.IsDictionary || entry.Values == null)
                return -1;

            var fieldValue = ReadFieldValue(property);
            if (fieldValue == null) return -1;

            for (int i = 0; i < entry.Values.Length; i++)
            {
                if (entry.Values[i] != null && entry.Values[i].Equals(fieldValue))
                    return i;
            }

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

        public override void PreRender(PropertyData data, PropertyAttribute attribute)
        {
            data.IsCustomField();
        }

        public override void RenderField(PropertyData data, PropertyAttribute attribute)
        {
            var attr = attribute as ValueSelectorAttribute;
            var entry = Resolve(data, attr);
            if (entry.HasError) return;

            var currentIndex = FindCurrentIndex(data.Property, entry);
            DrawingUtility.DrawSelector(data.Position, data.Property, entry.Keys, entry.Values, currentIndex, attr.Placeholder);
        }

        public override float RenderTopper(PropertyData data, PropertyAttribute attribute, bool onlyCalculation)
        {
            var entry = Resolve(data, attribute as ValueSelectorAttribute);
            if (!entry.HasError) return 0;
            var textHeight = DrawingUtility.CalcInfoBoxHeight(entry.Error, data.Position.width);
            if (!onlyCalculation)
                DrawingUtility.MakeInfoBox(data.Position, entry.Error, true);
            return textHeight;
        }
    }
}
#endif
