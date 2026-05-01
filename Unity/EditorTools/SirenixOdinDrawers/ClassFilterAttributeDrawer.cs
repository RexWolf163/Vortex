#if UNITY_EDITOR
using System;
using Sirenix.OdinInspector.Editor;
using Sirenix.Utilities.Editor;
using UnityEngine;
using Vortex.Unity.EditorTools.Attributes;
using Object = UnityEngine.Object;

namespace Vortex.Unity.EditorTools.SirenixOdinDrawers
{
    /// <summary>
    /// Odin-drawer для <see cref="ClassFilterAttribute"/>.
    /// Валидирует, что назначенный объект соответствует одному из RequiredTypes.
    /// Несоответствующие объекты сбрасываются в null с предупреждением в консоль.
    /// </summary>
    public sealed class ClassFilterAttributeDrawer : OdinAttributeDrawer<ClassFilterAttribute>
    {
        private bool _typeError;
        private string _errorMessage;

        protected override void Initialize()
        {
            var valueType = Property.Info.TypeOfValue;
            if (valueType == null || !typeof(Object).IsAssignableFrom(valueType))
            {
                _typeError = true;
                _errorMessage =
                    $"[ClassFilter] Field '{Property.Name}' is not a UnityEngine.Object. Attribute supports only ObjectReference fields.";
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

            CallNextDrawer(label);

            // Валидация после изменения
            var current = Property.ValueEntry.WeakSmartValue as Object;
            if (current == null) return;

            foreach (var requiredType in Attribute.RequiredTypes)
            {
                try
                {
                    if (IsValidObject(current, requiredType))
                        continue;

                    Debug.LogWarning(
                        $"[ClassFilter] Объект '{current.name}' не соответствует типу {requiredType.Name}. Поле очищено.");
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }

                Property.ValueEntry.WeakSmartValue = null;
                return;
            }
        }

        private static bool IsValidObject(Object obj, Type requiredType)
        {
            if (obj == null) return true;
            return obj switch
            {
                MonoBehaviour mb => requiredType.IsAssignableFrom(mb.GetType()),
                ScriptableObject so => requiredType.IsAssignableFrom(so.GetType()),
                _ => false,
            };
        }
    }
}
#endif