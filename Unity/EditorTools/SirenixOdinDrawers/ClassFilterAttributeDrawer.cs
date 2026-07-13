#if UNITY_EDITOR
using System;
using System.Collections;
using Sirenix.OdinInspector.Editor;
using Sirenix.Utilities.Editor;
using UnityEngine;
using Vortex.Unity.EditorTools.Attributes;
using Object = UnityEngine.Object;

namespace Vortex.Unity.EditorTools.SirenixOdinDrawers
{
    /// <summary>
    /// Odin-drawer для <see cref="ClassFilterAttribute"/>.
    /// Валидирует, что назначенное значение соответствует каждому из <see cref="ClassFilterAttribute.RequiredTypes"/>.
    ///
    /// Поддерживает как одиночное поле, так и КОЛЛЕКЦИЮ (массив / <see cref="System.Collections.Generic.List{T}"/>) —
    /// в этом случае фильтр применяется к каждому элементу.
    ///
    /// Две ветки логики в зависимости от типа поля:
    ///
    /// A. Поле — <see cref="Object">UnityEngine.Object</see>-ссылка (или коллекция таковых):
    /// 1. Если назначенный объект — <see cref="Component"/> или <see cref="GameObject"/>,
    ///    drawer берёт его <see cref="GameObject"/> и сканирует все компоненты до первого,
    ///    удовлетворяющего <see cref="ClassFilterAttribute.RequiredTypes"/> и совместимого с типом ссылки.
    /// 2. Если такой компонент найден — ссылка автоматически переключается на него (Debug.Log).
    /// 3. Если не найден — ссылка очищается в null (Debug.LogWarning).
    /// 4. <see cref="ScriptableObject"/>-ссылки проверяются напрямую — без GameObject-обхода.
    ///
    /// B. Поле — обычный managed-тип (класс/интерфейс, типично `[SerializeReference]`) или коллекция таковых:
    ///    компонентного поиска нет. Проверяется лишь, что фактический тип значения удовлетворяет всем
    ///    <see cref="ClassFilterAttribute.RequiredTypes"/> (наследование / реализация интерфейса);
    ///    при несовпадении ссылка обнуляется (Debug.LogWarning).
    /// </summary>
    public sealed class ClassFilterAttributeDrawer : OdinAttributeDrawer<ClassFilterAttribute>
    {
        private bool _typeError;
        private string _errorMessage;

        /// <summary>Тип ссылки, к которому проверяется совместимость (элемент для коллекций, само поле для одиночного).</summary>
        private Type _targetType;

        /// <summary>Поле — коллекция (массив/List) ссылок.</summary>
        private bool _isCollection;

        /// <summary>Целевой тип — <see cref="Object">UnityEngine.Object</see> (ветка A). Иначе managed-тип (ветка B).</summary>
        private bool _isUnityObject;

        protected override void Initialize()
        {
            var fieldType = Property.Info.TypeOfValue;
            _targetType = ResolveTargetType(fieldType, out _isCollection);

            if (_targetType == null)
            {
                _typeError = true;
                _errorMessage =
                    $"[ClassFilter] Field '{Property.Name}' is not a reference-type field (class/interface) " +
                    "nor an array/list of them. Attribute supports UnityEngine.Object references and " +
                    "managed reference types (e.g. [SerializeReference]).";
                return;
            }

            _isUnityObject = typeof(Object).IsAssignableFrom(_targetType);
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

            if (!_isCollection)
            {
                ValidateValue(Property.ValueEntry.WeakSmartValue,
                    value => Property.ValueEntry.WeakSmartValue = value);
                return;
            }

            // Коллекция: валидируем каждый элемент через его child-property.
            for (var i = 0; i < Property.Children.Count; i++)
            {
                var child = Property.Children[i];
                var current = child.ValueEntry?.WeakSmartValue;
                if (current == null) continue;
                ValidateValue(current, value => child.ValueEntry.WeakSmartValue = value);
            }
        }

        /// <summary>
        /// Диспетчер по ветке: UnityEngine.Object → компонентный резолв (A),
        /// managed-тип → простая проверка типа (B).
        /// </summary>
        private void ValidateValue(object current, Action<object> assign)
        {
            if (current == null) return;

            if (_isUnityObject)
                ValidateAndFix(current as Object, o => assign(o));
            else
                ValidatePlain(current, assign);
        }

        /// <summary>
        /// Определяет тип ссылки, к которому проверять совместимость: само поле (одиночное),
        /// либо тип элемента (для массива/списка). Принимаются ссылочные типы (класс/интерфейс) —
        /// как UnityEngine.Object, так и обычные managed-типы. Возвращает null для неподдерживаемых
        /// полей (например, value-типов).
        /// </summary>
        private static Type ResolveTargetType(Type fieldType, out bool isCollection)
        {
            isCollection = false;
            if (fieldType == null) return null;

            // Массив.
            if (fieldType.IsArray)
            {
                var element = fieldType.GetElementType();
                if (IsSupportedElement(element))
                {
                    isCollection = true;
                    return element;
                }
                return null;
            }

            // List<T> / иной generic IList<T>.
            if (fieldType.IsGenericType && typeof(IList).IsAssignableFrom(fieldType))
            {
                var args = fieldType.GetGenericArguments();
                if (args.Length == 1 && IsSupportedElement(args[0]))
                {
                    isCollection = true;
                    return args[0];
                }
                return null;
            }

            // Одиночное ссылочное поле (UnityEngine.Object либо managed-класс/интерфейс).
            return IsSupportedElement(fieldType) ? fieldType : null;
        }

        /// <summary>Поддерживаются ссылочные типы: классы и интерфейсы (value-типы отсекаются).</summary>
        private static bool IsSupportedElement(Type type) =>
            type != null && (type.IsClass || type.IsInterface);

        /// <summary>
        /// Ветка B (managed-тип): проверяет фактический тип значения против всех
        /// <see cref="ClassFilterAttribute.RequiredTypes"/>. При несовпадении хотя бы одного —
        /// обнуляет. Без GameObject/компонентного обхода.
        /// </summary>
        private void ValidatePlain(object current, Action<object> assign)
        {
            if (current == null) return;

            var actual = current.GetType();
            foreach (var requiredType in Attribute.RequiredTypes)
            {
                if (requiredType.IsAssignableFrom(actual)) continue;

                Debug.LogWarning(
                    $"[ClassFilter] значение типа '{actual.Name}' не удовлетворяет {requiredType.Name}. Ссылка очищена.");
                assign(null);
                return;
            }
        }

        /// <summary>
        /// Ветка A (UnityEngine.Object): проверяет одно значение по всем
        /// <see cref="ClassFilterAttribute.RequiredTypes"/>: при необходимости подменяет на
        /// подходящий компонент (через <paramref name="assign"/>) либо очищает в null.
        /// </summary>
        private void ValidateAndFix(Object current, Action<Object> assign)
        {
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
                            assign(resolved);
                            current = resolved;
                        }
                        continue;
                    }

                    Debug.LogWarning(
                        $"[ClassFilter] '{current.name}' не имеет компонента, удовлетворяющего {requiredType.Name}. Ссылка очищена.",
                        current);
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }

                assign(null);
                return;
            }
        }

        /// <summary>
        /// Пытается найти ссылку, удовлетворяющую <paramref name="requiredType"/> и присваиваемую
        /// <see cref="_targetType"/>. Возвращает <paramref name="resolved"/> = <paramref name="current"/>,
        /// если тот уже подходит; иначе сканирует компоненты GameObject до первого совпадения.
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

            // 1. Если целевой тип — GameObject-derived: значение остаётся GameObject,
            //    нам нужно лишь убедиться, что хотя бы один компонент удовлетворяет requiredType.
            if (typeof(GameObject).IsAssignableFrom(_targetType))
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

            // 2. Целевой тип — Component-derived. Если current сам подходит — возвращаем его.
            if (current is Component cmp
                && requiredType.IsAssignableFrom(cmp.GetType())
                && _targetType.IsAssignableFrom(cmp.GetType()))
            {
                resolved = cmp;
                return true;
            }

            // 3. Иначе — перебор всех компонентов GameObject до первого совпадения,
            //    которое одновременно удовлетворяет requiredType и присваиваемо к целевому типу.
            foreach (var comp in go.GetComponents<Component>())
            {
                if (comp == null) continue;
                if (!requiredType.IsAssignableFrom(comp.GetType())) continue;
                if (!_targetType.IsAssignableFrom(comp.GetType())) continue;
                resolved = comp;
                return true;
            }

            return false;
        }
    }
}
#endif
