#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Vortex.Unity.EditorTools.Abstraction;
using Vortex.Unity.EditorTools.Attributes;
using Vortex.Unity.EditorTools.Collections;
using Vortex.Unity.EditorTools.DomModel;
using Vortex.Unity.EditorTools.Elements;
using Object = UnityEngine.Object;

namespace Vortex.Unity.EditorTools.Bus
{
    /// <summary>
    /// DOM-based controller for multi-attribute rendering.
    ///
    /// При первом запросе свойства строит полное DOM-дерево для всего SerializedObject.
    ///
    /// Заменяет EditorToolsDrawersController с его time-based кешем (PageLifeTime ms).
    /// </summary>
    public static partial class InspectorController
    {
        /// <summary>
        /// Флаг необходимости полной пересборки DOM.
        /// Устанавливается при Undo, domain reload, внешних изменениях.
        /// </summary>
        private static bool _isDirty;

        /// <summary>
        /// Индекс страниц по InstanceID целевого объекта.
        /// </summary>
        private static readonly Dictionary<int, DomPage> Pages = new();

        /// <summary>
        /// Кеш drawer-экземпляров по типу атрибута.
        /// Drawer'ы stateless — один экземпляр на тип.
        /// </summary>
        private static Dictionary<Type, IMultiDrawerAttribute> _drawersCache;

        /// <summary>
        /// Флаг реентрантности: true пока идёт RecomputeAllHeights.
        /// Предотвращает рекурсию: ComputeNodeHeight → drawer.RenderTopper → GetPropertyWidth → GetNode → RecomputeAllHeights.
        /// </summary>
        private static bool _isRecomputing;

        #region PublicApi

        /// <summary>
        /// Отрисовка свойства через все зарегистрированные drawer'ы.
        /// Данные берутся из DOM-дерева, построенного в GetAttributeHeight.
        /// </summary>
        public static void RenderMultiAttribute(Rect position, SerializedProperty property, GUIContent label)
        {
            var node = GetNode(property);
            if (node == null) return;
            var prop = node.Data.Property;
            var lab = node.Data.Label;
            try
            {
                RenderNode(position, prop, lab, node);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }

            position.y += node.Data.Height;
        }

        #endregion

        #region DOM_Получение_построение

        /// <summary>
        /// Гарантирует наличие актуального DomNode для свойства.
        /// При необходимости строит страницу и/или пересчитывает высоты.
        /// </summary>
        private static DomNode GetNode(SerializedProperty property)
        {
            try
            {
                if (!IsPropertyValid(property))
                    return null;

                if (_drawersCache == null)
                    MakeDrawersCash();

                var targetObject = property.serializedObject.targetObject;
                var gameObject = (targetObject as Component)?.gameObject;
                var so = (targetObject as ScriptableObject);
                var pageId = gameObject != null ? gameObject.GetInstanceID() : targetObject.GetInstanceID();
                var currentFrame = Time.frameCount;
                var currentWidth = EditorGUIUtility.currentViewWidth;
                var componentId = targetObject.GetInstanceID();
                var nodeKey = MakeNodeKey(componentId, property.propertyPath);

                // Получаем или создаём страницу
                if (!Pages.TryGetValue(pageId, out var page))
                {
                    page = so != null ? new DomPage(so) : new DomPage(gameObject);
                    Pages[pageId] = page;
                }

                if (gameObject != null && page.Owner == null)
                {
                    page.Dispose();
                    page = new DomPage(gameObject);
                    Pages[pageId] = page;
                }

                // Инвалидация при resize окна, dirty-флаге или изменении количества компонентов
                var componentCount = gameObject != null ? gameObject.GetComponents<Component>().Length : 1;
                if (Math.Abs(page.ViewWidth - currentWidth) > 1f || _isDirty || page.ComponentCount != componentCount)
                {
                    page.ViewWidth = currentWidth;
                    page.ComponentCount = componentCount;
                    page.IsCalculated = false;
                }

                // Первый доступ, resize или dirty — строим структуру и сразу считаем ширины
                if (!page.IsCalculated)
                {
                    BuildPageStructure(page);
                    RecomputeAllHeights(page);
                    page.LastComputedFrame = currentFrame;
                    _isDirty = false;
                }
                // Новый фрейм — пересчитываем ширины (условия видимости, foldout-ы)
                else if (page.LastComputedFrame != currentFrame && !_isRecomputing)
                {
                    RecomputeAllHeights(page);
                    page.LastComputedFrame = currentFrame;
                }

                // Скроллбар: layout предыдущего кадра → если состояние изменилось, пересчитать ширины
                var scrollbar = IsInspectorScrollbarVisible();
                if (page.HasScrollbar != scrollbar)
                {
                    page.HasScrollbar = scrollbar;
                    RecomputeAllHeights(page);
                    //HandleUtility.Repaint();
                }

                // Ищем узел
                if (page.Nodes.TryGetValue(nodeKey, out var node))
                    return !IsPropertyValid(node.Data.Property) ? null : node;

                return null;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }

            return null;
        }

        /// <summary>
        /// Первичное построение: обходит все видимые свойства SerializedObject,
        /// создаёт DomNode с drawer-парами для каждого.
        /// </summary>
        private static void BuildPageStructure(DomPage page)
        {
            page.Reset();
            page.ViewWidth = EditorGUIUtility.currentViewWidth;

            page.CalculatedAtTime = DateTime.Now;

            //Если это page для  ScriptableObject - делаем массив на 1 элемент из этого SO

            Object[] components = page.SO != null ? new Object[] { page.SO } : page.Owner.GetComponents<Component>();

            foreach (var component in components)
            {
                if (component == null) continue; // Missing script
                if (component is Transform) continue; // Transform/RectTransform рендерится Unity нативно
                var componentId = component.GetInstanceID();
                var serializedObject = new SerializedObject(component);
                page.OwnedSerializedObjects.Add(serializedObject);

                // Контейнерная нода компонента — корень для всех его полей
                var componentNode = new DomNode(page, page.ViewWidth);
                var componentKey = MakeNodeKey(componentId, "");
                page.Nodes[componentKey] = componentNode;

                var iterator = serializedObject.GetIterator();

                var isFirstInScript = false;
                while (iterator.NextVisible(true))
                {
                    if (iterator.propertyPath == "m_Script")
                    {
                        isFirstInScript = true;
                        continue;
                    }

                    if (iterator.propertyPath.EndsWith(".Array.size")) continue;

                    var prop = serializedObject.FindProperty(iterator.propertyPath);
                    if (prop == null) continue;

                    var nodeKey = MakeNodeKey(componentId, prop.propertyPath);

                    // Parent: ищем по propertyPath, fallback на ноду компонента
                    var parentPath = GetParentPath(prop.propertyPath);
                    DomNode parent = parentPath != null
                                     && page.Nodes.TryGetValue(MakeNodeKey(componentId, parentPath), out var found)
                        ? found
                        : componentNode;

                    var node = CreateNode(page, parent, prop, new GUIContent(prop.displayName));
                    if (node == null) continue;
                    if (isFirstInScript)
                    {
                        node.IsFirstNode = true;
                        isFirstInScript = false;
                    }

                    if (prop.propertyType == SerializedPropertyType.ManagedReference)
                        isFirstInScript = true;

                    page.Nodes[nodeKey] = node;
                    if (!parent.Childrens.Contains(node))
                        parent.Childrens.Add(node);
                }

                var methods = component.GetType()
                    .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                foreach (var method in methods)
                {
                    var attrs = method.GetCustomAttributes(true);

                    foreach (var attr in attrs)
                    {
                        if (!_drawersCache.TryGetValue(attr.GetType(), out var drawer)) continue;

                        var nodeKey = MakeNodeKey(componentId, "$method:" + method.Name);

                        var node = new DomNode(page, componentNode, component, method);
                        page.Nodes[nodeKey] = node;
                        if (!componentNode.Childrens.Contains(node))
                            componentNode.Childrens.Add(node);
                        break;
                    }
                }
            }

            page.IsCalculated = true;

            var nodes = page.Nodes.OrderBy((p) => p.Value.Order).ToDictionary(p => p.Key, p => p.Value);
            page.Nodes.Clear();
            foreach (var node in nodes)
                page.Nodes.Add(node.Key, node.Value);

            foreach (var node in page.Nodes.Values)
                SortChildren(node);
        }

        private static void SortChildren(DomNode node)
        {
            var list = node.Childrens.OrderBy((p) => p.Order).ToList();
            node.Childrens.Clear();
            foreach (var item in list)
            {
                node.Childrens.Add(item);
                foreach (var children in item.Childrens)
                    SortChildren(children);
            }
        }

        /// <summary>
        /// Составной ключ: "componentInstanceId:propertyPath".
        /// Предотвращает коллизии одинаковых propertyPath у разных компонентов.
        /// </summary>
        private static string MakeNodeKey(int componentId, string propertyPath)
        {
            return $"{componentId}:{propertyPath}";
        }

        /// <summary>
        /// Создаёт DomNode: резолвит FieldInfo, собирает атрибуты, находит drawer'ы, вычисляет высоту.
        /// </summary>
        private static DomNode CreateNode(DomPage page, DomNode parent, SerializedProperty property, GUIContent label)
        {
            var fieldInfo = InspectorHandler.GetFieldInfo(property);
            if (fieldInfo == null)
                return null;

            var order = parent.Childrens.Count;
            var attributes = ReflectionCache.GetCustomAttributes(fieldInfo, true);
            foreach (var attribute in attributes)
            {
                if (attribute is not PositionAttribute attrPos) continue;
                order += attrPos.Priority;
            }

            // Элемент массива: имеет FieldInfo массива (для атрибутов), но рендерится как контейнер
            var isArrayElement = property.propertyPath.EndsWith("]");
            if (isArrayElement)
                return new DomNode(page, parent, property, label) { IsContainer = true, Order = order };

            return new DomNode(page, parent, property, label) { Order = order };
        }

        #endregion


        /// <summary>
        /// Cканирует сборки ru.vortex.* и строит индекс.
        /// </summary>
        private static void MakeDrawersCash()
        {
            _drawersCache = new Dictionary<Type, IMultiDrawerAttribute>();
            var propertyDrawerBaseType = typeof(PropertyDrawer);
            var propertyAttributeBaseType = typeof(PropertyAttribute);
            var multiDrawerInterface = typeof(IMultiDrawerAttribute);

            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (!asm.FullName.StartsWith("ru.vortex")) continue;

                foreach (var type in asm.GetTypes())
                {
                    if (type.IsAbstract || type.IsInterface) continue;
                    if (!propertyDrawerBaseType.IsAssignableFrom(type)) continue;
                    if (!multiDrawerInterface.IsAssignableFrom(type)) continue;

                    var cpdAttr = type.GetCustomAttribute<CustomPropertyDrawer>();
                    if (cpdAttr == null) continue;

                    var field = typeof(CustomPropertyDrawer).GetField("m_Type",
                        BindingFlags.NonPublic | BindingFlags.Instance);
                    var inspectedType = field?.GetValue(cpdAttr) as Type;

                    if (inspectedType == null || !propertyAttributeBaseType.IsAssignableFrom(inspectedType))
                        continue;

                    _drawersCache[inspectedType] = (IMultiDrawerAttribute)Activator.CreateInstance(type);
                }
            }
        }

        #region System

        /// <summary>
        /// Полный сброс всех страниц.
        /// </summary>
        public static void InvalidateAll()
        {
            foreach (var page in Pages.Values)
                page.Dispose();
            Pages.Clear();
            _isDirty = true;
        }

        [InitializeOnLoadMethod]
        private static void Initialize()
        {
            AssemblyReloadEvents.afterAssemblyReload += () =>
            {
                InvalidateAll();
                _drawersCache = null;
                MakeDrawersCash();
            };

            Undo.undoRedoPerformed -= InvalidateAll;
            Undo.undoRedoPerformed += InvalidateAll;
        }

        #endregion

        #region Utilities

        private static bool IsPropertyValid(SerializedProperty property)
        {
            if (property == null) return false;
            try
            {
                var so = property.serializedObject;
                return so?.targetObject != null && !string.IsNullOrEmpty(property.propertyPath);
            }
            catch
            {
                return false;
            }
        }

        private static bool ValuesEqual(object a, object b)
        {
            if (a == null && b == null) return true;
            if (a == null || b == null) return false;
            return a.Equals(b);
        }

        /// <summary>
        /// Извлекает путь родителя из propertyPath.
        /// "myStruct.field" → "myStruct"
        /// "list.Array.data[0]" → "list"
        /// "list.Array.data[0].field" → "list.Array.data[0]"
        /// "topLevelField" → null (корневой уровень)
        /// </summary>
        private static string GetParentPath(string propertyPath)
        {
            // Элемент массива: "list.Array.data[N]" → родитель "list"
            var arrayIndex = propertyPath.LastIndexOf(".Array.data[", StringComparison.Ordinal);
            if (arrayIndex >= 0)
            {
                var closingBracket = propertyPath.IndexOf(']', arrayIndex);
                // Путь заканчивается на "]" — это сам элемент, parent = массив
                if (closingBracket == propertyPath.Length - 1)
                    return propertyPath.Substring(0, arrayIndex);

                // Поле после "]." — parent = элемент массива
                return propertyPath.Substring(0, closingBracket + 1);
            }

            // Вложенное поле: "parent.child" → "parent"
            var dotIndex = propertyPath.LastIndexOf('.');
            if (dotIndex > 0)
                return propertyPath.Substring(0, dotIndex);

            // Корневое поле — родителя нет
            return null;
        }

        private static FieldInfo _scrollViewField;
        private static Type _inspectorType;
        private static bool _scrollReflectionResolved;

        public static bool IsInspectorScrollbarVisible()
        {
            if (!_scrollReflectionResolved)
            {
                _scrollReflectionResolved = true;
                _inspectorType = Type.GetType("UnityEditor.InspectorWindow, UnityEditor");
                if (_inspectorType?.BaseType != null)
                    _scrollViewField = _inspectorType.BaseType.GetField("m_ScrollView",
                        BindingFlags.NonPublic | BindingFlags.Instance);
            }

            if (_inspectorType == null || _scrollViewField == null)
                return false;

            var allInspectors = Resources.FindObjectsOfTypeAll(_inspectorType);
            if (allInspectors.Length == 0)
                return false;

            var scrollView = _scrollViewField.GetValue(allInspectors[0]) as ScrollView;
            if (scrollView == null)
                return false;

            var contentHeight = scrollView.contentContainer.layout.height;
            var viewportHeight = scrollView.contentViewport.layout.height;
            return contentHeight > viewportHeight;
        }

        private static void PreRender(PropertyData data)
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

        #endregion
    }
}

#endif