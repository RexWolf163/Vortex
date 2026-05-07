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
    /// Валидирует, что назначенный объект соответствует каждому из <see cref="ClassFilterAttribute.RequiredTypes"/>.
    ///
    /// Алгоритм при несовпадении типа:
    /// 1. Если назначенный объект — <see cref="Component"/> или <see cref="GameObject"/>,
    ///    drawer берёт его <see cref="GameObject"/> и сканирует все компоненты до первого,
    ///    удовлетворяющего <see cref="ClassFilterAttribute.RequiredTypes"/> и совместимого с типом поля.
    /// 2. Если такой компонент найден — поле автоматически переключается на него (Debug.Log).
    /// 3. Если не найден — поле очищается в null (Debug.LogWarning).
    /// 4. <see cref="ScriptableObject"/>-поля проверяются напрямую — без GameObject-обхода.
    /// </summary>
    public sealed class ClassFilterAttributeDrawer : OdinAttributeDrawer<ClassFilterAttribute>
    {
        private bool _typeError;
        private string _errorMessage;
        private Type _fieldType;

        protected override void Initialize()
        {
            _fieldType = Property.Info.TypeOfValue;
            if (_fieldType == null || !typeof(Object).IsAssignableFrom(_fieldType))
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

            var current = Property.ValueEntry.WeakSmartValue as Object;
            if (current == null) return;

            foreach (var requiredType in Attribute.RequiredTypes)
            {
                try
                {
                    if (TryResolve(current, requiredType, out var resolved))
                    {
                        if (!ReferenceEquals(current, resolved))
                        {
                            Debug.Log(
                                $"[ClassFilter] '{current.name}': взят компонент '{resolved.GetType().Name}', соответствующий {requiredType.Name}.",
                                resolved);
                            Property.ValueEntry.WeakSmartValue = resolved;
                            current = resolved;
                        }
                        continue;
                    }

                    Debug.LogWarning(
                        $"[ClassFilter] '{current.name}' не имеет компонента, удовлетворяющего {requiredType.Name}. Поле очищено.",
                        current);
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }

                Property.ValueEntry.WeakSmartValue = null;
                return;
            }
        }

        /// <summary>
        /// Пытается найти ссылку, удовлетворяющую <paramref name="requiredType"/> и присваиваемую
        /// типу поля. Возвращает <paramref name="resolved"/> = <paramref name="current"/>, если
        /// тот уже подходит; иначе сканирует компоненты GameObject до первого совпадения.
        /// </summary>
        private bool TryResolve(Object current, Type requiredType, out Object resolved)
        {
            resolved = null;
            if (current == null) return false;

            // ScriptableObject — отдельная ветка, без GameObject-обхода.
            if (current is ScriptableObject so)
            {
                if (!requiredType.IsAssignableFrom(so.GetType())) return false;
                resolved = so;
                return true;
            }

            // Источник GameObject для сканирования.
            GameObject go = null;
            if (current is GameObject directGo) go = directGo;
            else if (current is Component currentComponent) go = currentComponent.gameObject;
            if (go == null) return false;

            // 1. Если поле — GameObject-derived: значение остаётся GameObject,
            //    нам нужно лишь убедиться, что хотя бы один компонент удовлетворяет requiredType.
            if (typeof(GameObject).IsAssignableFrom(_fieldType))
            {
                foreach (var comp in go.GetComponents<Component>())
                {
                    if (comp == null) continue; // missing script
                    if (!requiredType.IsAssignableFrom(comp.GetType())) continue;
                    resolved = go;
                    return true;
                }
                return false;
            }

            // 2. Поле — Component-derived. Если current сам подходит — возвращаем его.
            if (current is Component cmp
                && requiredType.IsAssignableFrom(cmp.GetType())
                && _fieldType.IsAssignableFrom(cmp.GetType()))
            {
                resolved = cmp;
                return true;
            }

            // 3. Иначе — перебор всех компонентов GameObject до первого совпадения,
            //    которое одновременно удовлетворяет requiredType и присваиваемо к типу поля.
            foreach (var comp in go.GetComponents<Component>())
            {
                if (comp == null) continue;
                if (!requiredType.IsAssignableFrom(comp.GetType())) continue;
                if (!_fieldType.IsAssignableFrom(comp.GetType())) continue;
                resolved = comp;
                return true;
            }

            return false;
        }
    }
}
#endif
