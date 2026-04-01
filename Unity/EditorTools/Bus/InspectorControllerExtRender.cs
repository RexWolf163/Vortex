#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Vortex.Unity.EditorTools.Abstraction;
using Vortex.Unity.EditorTools.Collections;
using Vortex.Unity.EditorTools.DomModel;
using Vortex.Unity.EditorTools.Elements;

namespace Vortex.Unity.EditorTools.Bus
{
    public static partial class InspectorController
    {
        private static void RenderNode(Rect position, SerializedProperty property, GUIContent label, DomNode node)
        {
            var data = node.Data;
            var w = node.Data.Width;
            var shift = position.width - w;

            position.width = w;
            position.x += shift;
            data.Update(position, label);

            var before = InspectorHandler.GetPropertyValue(property);
            var fieldInfo = data.FieldInfo;
            var attributes = ReflectionCache.GetCustomAttributes(fieldInfo, true);
            var propAttrs = new List<(PropertyAttribute attr, IMultiDrawerAttribute drawer)>();
            foreach (var attr in attributes)
            {
                if (attr is not PropertyAttribute propAttr) continue;
                if (!_drawersCache.TryGetValue(propAttr.GetType(), out var drawer)) continue;
                propAttrs.Add((propAttr, drawer));
            }

            if (propAttrs.Count == 0)
            {
                if (node.Childrens.Count > 0)
                    CollectionRenderer.DrawCollection(data.Position, property, data.Label, data.FieldInfo);
                else
                    EditorGUI.PropertyField(position, property, label, true);
                return;
            }

            // 1. Toppers
            if (data.IsLabelVisible || data.IsFieldVisible)
            {
                foreach (var (propAttr, drawer) in propAttrs)
                {
                    var topperHeight = drawer.RenderTopper(data, propAttr, false);
                    if (topperHeight == 0) continue;

                    var pos = data.Position;
                    pos.y += topperHeight;
                    data.Position = pos;
                }
            }

            // Rect для поля
            var dataPos = data.Position;
            dataPos.height = data.BaseHeight;
            data.Position = dataPos;

            // 2. Label
            if (data.IsLabelVisible)
            {
                if (data.IsLabelDefault)
                {
                    var labelRect = new Rect(data.Position.x, data.Position.y,
                        EditorGUIUtility.labelWidth, data.Position.height);
                    GUI.Label(labelRect, label);
                    data.Position = new Rect(
                        data.Position.x + EditorGUIUtility.labelWidth + DrawingUtility.Padding,
                        data.Position.y, data.Position.width - EditorGUIUtility.labelWidth,
                        data.Position.height);
                }
                else
                {
                    foreach (var (propAttr, drawer) in propAttrs)
                        drawer.RenderLabel(data, propAttr);
                }
            }

            // 3. Field
            if (data.IsFieldVisible)
            {
                EditorGUI.BeginChangeCheck();

                foreach (var (propAttr, drawer) in propAttrs)
                    drawer.RenderField(data, propAttr);

                if (data.IsFieldDefault)
                    DrawingUtility.DrawDefaultField(data.Position, data, property);
            }

            if (data.IsFieldVisible && EditorGUI.EndChangeCheck())
            {
                property.serializedObject.ApplyModifiedProperties();
                var after = InspectorHandler.GetPropertyValue(property);
                if (!ValuesEqual(before, after))
                    ReflectionHelper.InvokeOnValueChanged(property);
            }
        }
    }
}
#endif