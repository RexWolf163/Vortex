#if UNITY_EDITOR
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Vortex.Unity.EditorTools.Abstraction;
using Vortex.Unity.EditorTools.Attributes;
using Vortex.Unity.EditorTools.Collections;
using Vortex.Unity.EditorTools.Elements;

namespace Vortex.Unity.EditorTools.AttributeDrawers
{
    /// <summary>
    /// IMultiDrawerAttribute-drawer для [VortexCollection].
    /// Перехватывает рендеринг поля-коллекции и передаёт его в CollectionRenderer.
    /// Toppers (InfoBubble и др.) обрабатываются своими drawer'ами через pipeline.
    /// </summary>
    [CustomPropertyDrawer(typeof(VortexCollectionAttribute))]
    public sealed class VortexCollectionMultiDrawer : MultiDrawer
    {
        public override void PreRender(PropertyData data, PropertyAttribute attribute)
        {
            var property = data.Property;
            if (property == null || !property.isArray) return;

            // Коллекция рендерит свой header с label — отключаем дефолтные
            data.IsCustomField();
            data.IsCustomLabel();

            // Заменяем дефолтную высоту Unity на высоту CollectionRenderer
            var collectionHeight = CollectionRenderer.GetCollectionHeight(property, data.Width, data.FieldInfo);
            data.AddHeight(collectionHeight - data.BaseHeight);
            data.BaseHeight = collectionHeight;

            // Резолвим LabelText, если есть
            var labelAttr = data.FieldInfo?.GetCustomAttribute<LabelTextAttribute>(true);
            if (labelAttr != null)
            {
                var (text, _) = ReflectionHelper.ResolveTextOrMethod(property, labelAttr.TextOrMethod);
                if (!string.IsNullOrEmpty(text))
                    data.Label = new GUIContent(text);
            }
        }

        public override void RenderField(PropertyData data, PropertyAttribute attribute)
        {
            var property = data.Property;
            if (property == null || !property.isArray) return;

            var rect = data.Position;
            rect.height = data.BaseHeight;

            CollectionRenderer.DrawCollection(rect, property, data.Label, data.FieldInfo);
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return base.GetPropertyHeight(property, label);
        }
    }
}
#endif