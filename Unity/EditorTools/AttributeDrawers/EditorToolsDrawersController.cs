/*#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Vortex.Unity.EditorTools.Abstraction;
using Vortex.Unity.EditorTools.Elements;

namespace Vortex.Unity.EditorTools.AttributeDrawers
{
    /// <summary>
    /// Контроллер управления отрисовкой нескольких PropertyAttribute на одном поле
    ///
    /// Логика работы - вызываются по очереди все атрибуты
    /// Первый круг - формирование топпинга (информационные поля над полем)
    /// Второй круг - заполнение метки. Метку заполняет только самый первый drawer претендующий на нее
    /// Третий круг - заполнение поля по Rect в зависимости от метки; 
    /// </summary>
    public static class EditorToolsDrawersController
    {
        /// <summary>
        /// Кеш экземпляров drawer'ов по типу атрибута.
        /// Drawer'ы stateless (контекст передаётся через PropertyData),
        /// поэтому один экземпляр переиспользуется для всех полей.
        /// </summary>
        private static Dictionary<Type, IMultiDrawerAttribute> _drawersCache;

        /// <summary>
        /// Индекс переиспользуемых PropertyData
        /// Слот сбрасывается при первом запросе высоты или при завершении рендеринга 
        /// </summary>
        private static readonly Dictionary<PropertyKey, PropertyData> SharedData = new();

        // Переиспользуемый список drawer-пар для текущего поля
        private static readonly
            Dictionary<PropertyKey, List<(PropertyAttribute attribute, IMultiDrawerAttribute drawer)>>
            SharedDrawerList = new();

        private static readonly Dictionary<PropertyKey, float> ElementWidth = new();

        /// <summary>
        /// отметка времени последней отрисовки
        /// </summary>
        private static DateTime _lastDrawTime;

        public static float GetAttributeHeight(SerializedProperty property, GUIContent label)
        {
            if ((DateTime.Now - _lastDrawTime).TotalMilliseconds > 100)
            {
                _lastDrawTime = DateTime.Now;
                SharedData.Clear();
                SharedDrawerList.Clear();
                ElementWidth.Clear();
            }

            if (!SharedData.TryGetValue(property, out var sharedData) || sharedData.Height == 0)
                CalculateHeight(property, label);
            return SharedData[property].Height;
        }

        public static float GetPropertyWidth(SerializedProperty property)
        {
            if (ElementWidth.TryGetValue(property, out var width)) return width;

            var approxWidth = EditorGUIUtility.currentViewWidth;
            var stage = property.propertyPath.Split(".Array.").Length;
            approxWidth -= stage * 5f * DrawingUtility.Padding - 40f;
            RegistrationPropertyWidth(property, approxWidth);

            ElementWidth[property] = width;

            return width;
        }

        public static void RegistrationPropertyWidth(SerializedProperty property, float width)
        {
            ElementWidth[property] = width;
        }

        private static bool IsPropertyValid(SerializedProperty property)
        {
            if (property == null)
                return false;
            try
            {
                var serializedObject = property.serializedObject;
                if (serializedObject == null)
                    return false;

                var targetObject = serializedObject.targetObject;
                if (targetObject == null)
                    return false;

                // Дополнительно проверяем, что путь не пустой
                return !string.IsNullOrEmpty(property.propertyPath);
            }
            catch (Exception e)
            {
                return false;
            }
        }

        /// <summary>
        /// Вычисляем высоту проводя симуляцию рендеринга
        /// </summary>
        /// <param name="property"></param>
        /// <param name="label"></param>
        private static void CalculateHeight(SerializedProperty property, GUIContent label)
        {
            /*
            try
            {
                if (!SharedData.TryGetValue(property, out var sharedData))
                {
                    sharedData = new PropertyData();
                    SharedData.Add(property, sharedData);
                    SharedDrawerList[property] =
                        new List<(PropertyAttribute attribute, IMultiDrawerAttribute drawer)>();
                }

                if (IsPropertyValid(sharedData.Property) && sharedData.Height != 0)
                    return;

                sharedData.AddHeight(EditorGUIUtility.singleLineHeight);

                var defaultLine = EditorGUI.GetPropertyHeight(property, label, true);
                sharedData.BaseHeight = defaultLine;
                sharedData.AddHeight(-EditorGUIUtility.singleLineHeight + defaultLine);
                var fieldInfo = ToolsController.GetFieldInfo(property);
                if (fieldInfo == null)
                    return;

                // Кешированные атрибуты
                var attributes = ReflectionCache.GetCustomAttributes(fieldInfo, true);

                var width = GetPropertyWidth(property);

                sharedData.Reset(new Rect(0, 0, width, 0), label, property, fieldInfo);
                sharedData.AddHeight(defaultLine == 0 ? EditorGUIUtility.singleLineHeight : defaultLine);

                (PropertyAttribute attribute, IMultiDrawerAttribute drawer)? labelDrawer = null;

                // Переиспользуем SharedDrawerList вместо new List<>()
                var sharedDrawerList = SharedDrawerList[property];
                sharedDrawerList.Clear();
                foreach (var attribute in attributes)
                {
                    var drawer = GetAttributeDrawer(attribute.GetType());
                    if (attribute is not PropertyAttribute propAttr || drawer == null) continue;

                    sharedDrawerList.Add((propAttr, drawer));
                    drawer.PreRender(sharedData, propAttr);

                    if (labelDrawer == null && !sharedData.IsLabelDefault)
                        labelDrawer = sharedDrawerList[^1];
                }

                sharedData.SetLabelDrawer(labelDrawer);

                if (sharedData.IsLabelVisible || sharedData.IsFieldVisible)
                    //рисуем шапку
                    foreach (var (attribute, drawer) in sharedDrawerList)
                    {
                        var add = drawer.RenderTopper(sharedData, attribute, true);
                        if (add == 0) continue;
                        var pos = sharedData.Position;
                        pos.y += add;
                        sharedData.Position = pos;
                        sharedData.AddHeight(add);
                    }

                sharedData.Position.height = sharedData.BaseHeight;

                if (!sharedData.IsFieldVisible && !sharedData.IsLabelVisible)
                    sharedData.AddHeight(-defaultLine);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        #1#
        }

        /// <summary>
        /// Запускает отрисовку для всех атрибутов на поле
        /// 
        /// Возвращает итоговую высоту поля.
        /// </summary>
        public static void RenderMultiAttribute(Rect position, SerializedProperty property, GUIContent label,
            PropertyDrawer caller)
        {
            /*try
            {
                if (!SharedData.TryGetValue(property, out var sharedData))
                    CalculateHeight(property, label);

                sharedData = SharedData[property];

                var defaultLine = sharedData.BaseHeight;
                var fieldInfo = ToolsController.GetFieldInfo(property);
                if (fieldInfo == null)
                {
                    EditorGUI.PropertyField(position, property, label, true);
                    return;
                }

                // Переиспользуем SharedData вместо new PropertyData()
                var sharedDrawerList = SharedDrawerList[property];
                if (IsPropertyValid(sharedData.Property)
                    && sharedData.Height != 0
                    && sharedData.Property.propertyPath == property.propertyPath)
                    sharedData.Update(position, label);
                else
                {
                    sharedData.Reset(position, label, property, fieldInfo);
                    sharedDrawerList.Clear();
                }

                var before = ToolsController.GetPropertyValue(property);

                var add = 0f;
                if (sharedData.IsLabelVisible || sharedData.IsFieldVisible)
                    //рисуем шапку
                    foreach (var (attribute, drawer) in sharedDrawerList)
                    {
                        add = drawer.RenderTopper(sharedData, attribute, false);

                        if (add == 0) continue;

                        var pos = sharedData.Position;
                        pos.y += add;
                        sharedData.Position = pos;
                    }

                var dataPos = sharedData.Position;
                dataPos.height = defaultLine;
                sharedData.Position = dataPos;

                var labelDrawer = sharedData.LabelDrawer;
                //рисуем ярлык
                if (sharedData.IsLabelVisible)
                {
                    if (sharedData.IsLabelDefault || labelDrawer == null)
                    {
                        var labelRect = new Rect(sharedData.Position.x, sharedData.Position.y,
                            EditorGUIUtility.labelWidth, sharedData.Position.height);
                        EditorGUI.PrefixLabel(labelRect, label);
                        sharedData.Position = new Rect(
                            sharedData.Position.x + EditorGUIUtility.labelWidth,
                            sharedData.Position.y,
                            sharedData.Position.width - EditorGUIUtility.labelWidth,
                            sharedData.Position.height);
                    }
                    else
                    {
                        labelDrawer.Value.drawer.RenderLabel(sharedData, labelDrawer.Value.attribute);
                    }
                }

                //рисуем поле
                if (sharedData.IsFieldVisible)
                {
                    EditorGUI.BeginChangeCheck();
                    foreach (var tuple in sharedDrawerList)
                        tuple.drawer.RenderField(sharedData, tuple.attribute);

                    if (sharedData.IsFieldDefault)
                        DrawingUtility.DrawDefaultField(sharedData.Position, sharedData, property);
                }

                if (sharedData.IsFieldVisible && EditorGUI.EndChangeCheck())
                {
                    property.serializedObject.ApplyModifiedProperties();
                    var after = ToolsController.GetPropertyValue(property);
                    if (!ValuesEqual(before, after))
                        ReflectionHelper.InvokeOnValueChanged(property);
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }#1#
        }

        private static bool ValuesEqual(object a, object b)
        {
            if (a == null && b == null) return true;
            if (a == null || b == null) return false;
            return a.Equals(b);
        }

        /// <summary>
        /// Возвращает дровер сопоставленный атрибуту.
        /// Если индекс пустой - заполняет его
        /// </summary>
        private static IMultiDrawerAttribute GetAttributeDrawer(Type attributeType)
        {
            if (_drawersCache != null)
            {
                _drawersCache.TryGetValue(attributeType, out var cached);
                return cached;
            }

            _drawersCache = new Dictionary<Type, IMultiDrawerAttribute>();
            var propertyDrawerBaseType = typeof(PropertyDrawer);
            var propertyAttributeBaseType = typeof(PropertyAttribute);
            var multiDrawerInterface = typeof(IMultiDrawerAttribute);

            var drawerTypes = AppDomain.CurrentDomain.GetAssemblies().Where(a => a.FullName.StartsWith("ru.vortex"))
                .SelectMany(a => a.GetTypes())
                .Where(t => !t.IsAbstract
                            && !t.IsInterface
                            && propertyDrawerBaseType.IsAssignableFrom(t)
                            && multiDrawerInterface.IsAssignableFrom(t));
            foreach (var drawerType in drawerTypes)
            {
                var cpdAttr = drawerType.GetCustomAttribute<CustomPropertyDrawer>();
                if (cpdAttr == null)
                    continue;

                var field = typeof(CustomPropertyDrawer).GetField("m_Type",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                var inspectedType = field?.GetValue(cpdAttr) as Type;

                if (inspectedType == null)
                    continue;

                if (!propertyAttributeBaseType.IsAssignableFrom(inspectedType))
                    continue;

                _drawersCache[inspectedType] = (IMultiDrawerAttribute)Activator.CreateInstance(drawerType);
            }

            return GetAttributeDrawer(attributeType);
        }
    }
}
#endif*/