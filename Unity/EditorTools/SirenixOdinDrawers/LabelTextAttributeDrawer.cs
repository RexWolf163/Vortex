#if UNITY_EDITOR
using Sirenix.OdinInspector.Editor;
using Sirenix.OdinInspector.Editor.ValueResolvers;
using UnityEngine;
using Vortex.Unity.EditorTools.Attributes;

namespace Vortex.Unity.EditorTools.SirenixOdinDrawers
{
    /// <summary>
    /// Odin-drawer для <see cref="LabelTextAttribute"/>.
    /// Подменяет label поля на статическую строку или результат метода/проперти.
    /// </summary>
    public sealed class LabelTextAttributeDrawer : OdinAttributeDrawer<LabelTextAttribute>
    {
        private ValueResolver<string> _resolver;

        protected override void Initialize()
        {
            _resolver = ValueResolver.GetForString(Property, Attribute.TextOrMethod);
        }

        protected override void DrawPropertyLayout(GUIContent label)
        {
            if (_resolver.HasError)
            {
                _resolver.DrawError();
                CallNextDrawer(label);
                return;
            }

            var resolved = _resolver.GetValue();
            if (label == null)
                CallNextDrawer(null);
            else
                CallNextDrawer(new GUIContent(resolved, label.tooltip));
        }
    }
}
#endif