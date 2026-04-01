#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Vortex.Unity.EditorTools.DomModel;
using Vortex.Unity.EditorTools.Elements;

namespace Vortex.Unity.EditorTools.Bus
{
    public static partial class InspectorController
    {
        /// <summary>
        /// Возвращает предрассчитанную высоту свойства с учётом всех атрибутов.
        /// При первом вызове для данного SerializedObject строит полное DOM-дерево.
        /// </summary>
        public static float GetAttributeHeight(SerializedProperty property, GUIContent label)
        {
            var node = GetNode(property);
            if (node == null || node.IsContainer)
                return EditorGUI.GetPropertyHeight(property, label, true);

            if (node.Data.Height == 0)
                ComputeNodeHeight(node);
            return node.Data.Height;
        }

        private const float ScrollbarWidth = 14f;

        private static void ComputeNodeWidthRecursive(DomNode node)
        {
            if (node.IsContainer)
            {
                var isRootContainer = node.Parent == null;
                var pageWidth = node.Page.ViewWidth - (node.Page.HasScrollbar ? ScrollbarWidth : 0f);
                node.Data.Width = isRootContainer
                    ? pageWidth
                    : node.Parent.Data.Width - 5f * DrawingUtility.Padding - 10f;
            }
            else
                node.Data.Width = CalculateWidth(node);

            foreach (var child in node.Childrens)
                ComputeNodeWidthRecursive(child);
        }

        private static float CalculateWidth(DomNode node)
        {
            // Reset обнуляет Height, восстанавливает флаги видимости
            node.Data.Reset();

            return (node.Parent.Parent != null)
                ? node.Parent.Data.Width - 6f * DrawingUtility.Padding
                : (node.Page.ViewWidth - (node.Page.HasScrollbar ? ScrollbarWidth : 0f)) - 6f * DrawingUtility.Padding;
        }

        /// <summary>
        /// Вычисляет высоту одного узла
        /// </summary>
        private static void ComputeNodeHeight(DomNode node)
        {
            var data = node.Data;
            var property = data.Property;

            // Пропускаем ноды с мёртвым SerializedObject (динамические ноды от transient SO)
            if (!IsPropertyValid(property)) return;

            var fieldInfo = data.FieldInfo;
            if (fieldInfo == null) return;

            var defaultHeight = EditorGUIUtility.singleLineHeight;
            if (node.Childrens is { Count: > 0 })
            {
                PreRender(data);
                defaultHeight = data.BaseHeight;
            }
            else
            {
                data.BaseHeight = defaultHeight;
                data.AddHeight(defaultHeight);
            }

            var attributes = ReflectionCache.GetCustomAttributes(fieldInfo, true);
            var propAttrs = new List<PropertyAttribute>();
            foreach (var attr in attributes)
            {
                if (attr is HideInInspector)
                {
                    data.HideField();
                    data.HideLabel();
                    break;
                }

                if (attr is not PropertyAttribute propAttr)
                    continue;
                if (!_drawersCache.TryGetValue(propAttr.GetType(), out var drawer))
                    continue;
                propAttrs.Add(propAttr);
                drawer.PreRender(data, propAttr);
            }

            // Toppers — информационные блоки над полем
            if (data.IsLabelVisible || data.IsFieldVisible)
            {
                foreach (var attribute in propAttrs)
                {
                    if (!_drawersCache.TryGetValue(attribute.GetType(), out var drawer))
                        continue;
                    var topperHeight = drawer.RenderTopper(data, attribute, true);
                    if (topperHeight == 0) continue;

                    var pos = data.Position;
                    pos.y += topperHeight;
                    data.Position = pos;
                    data.AddHeight(topperHeight);
                }
            }

            data.Position = new Rect(data.Position.x, data.Position.y,
                data.Position.width, data.BaseHeight);

            // Скрытое поле — вычитаем высоту
            if (!data.IsFieldVisible && !data.IsLabelVisible)
                data.AddHeight(-defaultHeight);
        }

        /// <summary>
        /// Примерная ширина поля с учётом вложенности.
        /// </summary>
        public static float GetPropertyWidth(SerializedProperty property)
        {
            var node = GetNode(property);
            if (node == null)
                return EditorGUIUtility.fieldWidth;

            return node.Data.Width;
        }

        private static void RecomputeAllHeights(DomPage page)
        {
            _isRecomputing = true;
            try
            {
                // Обновляем owned SO, чтобы видеть изменения из основного инспектора
                foreach (var so in page.OwnedSerializedObjects)
                {
                    if (so == null || so.targetObject == null) continue;
                    so.Update();
                }

                // Рекурсивно от корней вниз выставление ширины
                foreach (var kvp in page.Nodes)
                {
                    if (kvp.Value.Parent == null)
                        ComputeNodeWidthRecursive(kvp.Value);
                }
            }
            finally
            {
                _isRecomputing = false;
            }
        }
    }
}
#endif