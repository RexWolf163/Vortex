#if UNITY_EDITOR
using System.Reflection;
using Sirenix.OdinInspector.Editor;
using Sirenix.Utilities.Editor;
using UnityEngine;
using Vortex.Unity.EditorTools.Attributes;
using Object = UnityEngine.Object;

namespace Vortex.Unity.EditorTools.SirenixOdinDrawers
{
    /// <summary>
    /// Odin-drawer для <see cref="AutoLinkAttribute"/>.
    /// Если поле UnityEngine.Object и == null — автоматически линкует компонент с того же GameObject.
    /// Учитывает <see cref="ClassFilterAttribute"/> для фильтрации по типу.
    /// </summary>
    public sealed class AutoLinkAttributeDrawer : OdinAttributeDrawer<AutoLinkAttribute, Object>
    {
        private bool _typeError;
        private string _errorMessage;
        private FieldInfo _fieldInfo;

        protected override void Initialize()
        {
            _fieldInfo = Property.Info.GetMemberInfo() as FieldInfo;
            if (_fieldInfo == null || !typeof(Object).IsAssignableFrom(_fieldInfo.FieldType))
            {
                _typeError = true;
                _errorMessage =
                    $"[AutoLink] Field '{Property.Name}' is not a UnityEngine.Object. AutoLink supports only Component/GameObject/Object fields.";
            }
        }

        protected override void DrawPropertyLayout(GUIContent label)
        {
            if (_typeError)
            {
                SirenixEditorGUI.ErrorMessageBox(_errorMessage);
                CallNextDrawer(label);
                return;
            }

            var current = this.ValueEntry.WeakSmartValue as Object;
            if (current == null)
                TryAutoLink();

            CallNextDrawer(label);
        }

        private void TryAutoLink()
        {
            var target = Property.SerializationRoot.ValueEntry.WeakSmartValue as Object;
            if (target == null) return;

            GameObject gameObject = target switch
            {
                Component c => c.gameObject,
                GameObject g => g,
                _ => null,
            };
            if (gameObject == null) return;

            var fieldType = _fieldInfo.FieldType;
            var classFilter = _fieldInfo.GetCustomAttribute<ClassFilterAttribute>();

            if (classFilter != null)
            {
                foreach (var requiredType in classFilter.RequiredTypes)
                {
                    var candidates = gameObject.GetComponents(fieldType);
                    foreach (var candidate in candidates)
                    {
                        if (!requiredType.IsInstanceOfType(candidate)) continue;
                        ValueEntry.WeakSmartValue = candidate;
                        return;
                    }
                }

                return;
            }

            var found = gameObject.GetComponent(fieldType);
            if (found != null)
                ValueEntry.WeakSmartValue = found;
        }
    }
}
#endif