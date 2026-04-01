#if UNITY_EDITOR
using System;
using System.Collections;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Vortex.Unity.EditorTools.AttributeDrawers;
using Vortex.Unity.EditorTools.Attributes;
using Vortex.Unity.EditorTools.EditorSettings;
using Vortex.Unity.EditorTools.Elements;

namespace Vortex.Unity.EditorTools.Collections
{
    public class CollectionRenderer
    {
        // ════════════════════════════════════════════════════════
        //  Drag & Drop состояние
        // ════════════════════════════════════════════════════════

        private static string _dragPropertyPath;
        private static int _dragSourceIndex = -1;
        private static int _dragDropTarget = -1;
        private static bool _isDragging;
        private static int _dragControlId;
        private static float[] _elementYPositions;

        private static void ResetDragState()
        {
            if (_isDragging && GUIUtility.hotControl == _dragControlId)
                GUIUtility.hotControl = 0;

            _dragPropertyPath = null;
            _dragSourceIndex = -1;
            _dragDropTarget = -1;
            _isDragging = false;
            _dragControlId = 0;
        }

        /// <summary>
        /// Callback для структурных изменений, вызываемый при Drag&Drop перемещении.
        /// Позволяет вызывающему коду (например, Odin-drawer) обработать перемещение
        /// своим способом, минуя MoveArrayElement.
        /// Параметры: SerializedProperty (array), int srcIndex, int dstIndex.
        /// </summary>
        public static Action<SerializedProperty, int, int> OnMoveElement;

        public static void DrawCollection(Rect position, SerializedProperty property, GUIContent label,
            FieldInfo fieldInfo = null)
        {
            DrawingUtility.CheckThemeChange();
            var currentY = position.y;
            var isManagedRef = TypeResolver.IsManagedReferenceField(fieldInfo)
                               || (property.arraySize > 0 && property.GetArrayElementAtIndex(0).propertyType ==
                                   SerializedPropertyType.ManagedReference);

            var rect = new Rect(position.x - 1f, position.y - 1f, position.width + 2f, position.height);
            EditorGUI.DrawRect(rect, ToolsSettings.GetBgColor(DefaultColors.BoxBg));
            DrawingUtility.DrawBoxBorder(rect, ToolsSettings.GetLineColor(DefaultColors.ToggleBoxBorder),
                drawTop: false);
            rect = new Rect(position.x - 1f, position.y - 1f, position.width + 2f, DrawingUtility.RowHeight + 2f);
            DrawingUtility.DrawBoxBorder(rect, ToolsSettings.GetBgColor(DefaultColors.BorderColor),
                ToolsSettings.GetBgColor(DefaultColors.BorderColorLight));

            var foldoutRect = new Rect(position.x, currentY, position.width, DrawingUtility.RowHeight);
            property.isExpanded = DrawCollectionHeader(foldoutRect, property, label, isManagedRef);

            if (!property.isExpanded)
                return;

            currentY += DrawingUtility.RowHeight + DrawingUtility.ElementSpacing / 2f;

            var arraySize = property.arraySize;
            var isDragActive = _isDragging && _dragPropertyPath == property.propertyPath;

            // Запоминаем Y-позиции элементов для определения drop target
            if (_elementYPositions == null || _elementYPositions.Length < arraySize + 1)
                _elementYPositions = new float[arraySize + 1];

            var indexToRemove = -1;
            for (int i = 0; i < arraySize; i++)
            {
                _elementYPositions[i] = currentY;
                var element = property.GetArrayElementAtIndex(i);
                bool isNull = isManagedRef && TypeResolver.IsElementNull(element);
                bool isSimple = !isNull && !element.hasVisibleChildren;

                if (isNull)
                {
                    var r = new Rect(position.x, currentY, position.width, DrawingUtility.RowHeight);
                    DrawNullElement(r, element, property, i, fieldInfo, ref indexToRemove);
                    currentY += DrawingUtility.RowHeight + DrawingUtility.ElementSpacing;
                }
                else if (isSimple)
                {
                    var r = new Rect(position.x, currentY, position.width, DrawingUtility.RowHeight);
                    DrawSimpleElement(r, element, property, i, ref indexToRemove);
                    currentY += DrawingUtility.RowHeight + DrawingUtility.ElementSpacing;
                }
                else
                {
                    var h = GetElementContentHeight(element);
                    var r = new Rect(position.x, currentY, position.width, h);
                    DrawNormalElement(r, element, property, i, isManagedRef, ref indexToRemove);
                    currentY += h + DrawingUtility.ElementSpacing;
                }
            }

            _elementYPositions[arraySize] = currentY;

            // Drag & Drop логика
            if (isDragActive && arraySize > 1)
            {
                HandleDragDrop(position, property, arraySize);
            }

            // Сброс drag при mouse up вне зоны drag (безопасность, только для своей коллекции)
            if (_isDragging && _dragPropertyPath == property.propertyPath
                            && Event.current.type == EventType.MouseUp && Event.current.button == 0)
            {
                ResetDragState();
                Event.current.Use();
            }

            if (indexToRemove >= 0)
            {
                var removeIdx = indexToRemove;
                var propPath = property.propertyPath;
                var so = property.serializedObject;

                EditorApplication.delayCall += () =>
                {
                    if (so == null || so.targetObject == null) return;
                    so.Update();
                    var freshProp = so.FindProperty(propPath);
                    if (freshProp == null || !freshProp.isArray) return;
                    if (removeIdx >= freshProp.arraySize) return;
                    freshProp.DeleteArrayElementAtIndex(removeIdx);
                    so.ApplyModifiedProperties();
                };
            }
        }

        internal static float GetCollectionHeight(SerializedProperty property, float rectWidth,
            FieldInfo fieldInfo = null)
        {
            var h = DrawingUtility.RowHeight + 2f;
            if (!property.isExpanded) return h;

            h += DrawingUtility.Padding;

            var isManagedRef = TypeResolver.IsManagedReferenceField(fieldInfo)
                               || (property.arraySize > 0 && property.GetArrayElementAtIndex(0).propertyType ==
                                   SerializedPropertyType.ManagedReference);

            for (var i = 0; i < property.arraySize; i++)
            {
                var el = property.GetArrayElementAtIndex(i);
                var isNull = isManagedRef && TypeResolver.IsElementNull(el);
                var isSimple = !isNull && !el.hasVisibleChildren;

                if (isNull || isSimple)
                    h += DrawingUtility.RowHeight + DrawingUtility.ElementSpacing;
                else
                    h += GetElementContentHeight(el) + DrawingUtility.ElementSpacing;
            }

            return h - DrawingUtility.ElementSpacing - 1f;
        }

        // ════════════════════════════════════════════════════════
        //  Элементы
        // ════════════════════════════════════════════════════════

        private static void DrawSimpleElement(Rect boxRect, SerializedProperty element,
            SerializedProperty arrayProp, int index, ref int indexToRemove)
        {
            var isDragSource = _isDragging && _dragPropertyPath == arrayProp.propertyPath &&
                               _dragSourceIndex == index;

            var color = isDragSource
                ? ToolsSettings.GetBgColor(DefaultColors.DragIndicator)
                : ToolsSettings.GetBgColor(DefaultColors.BoxBg);
            color = DrawingUtility.ApplyOddCorrection(color, index);
            EditorGUI.DrawRect(boxRect, color);
            DrawingUtility.DrawBoxBorder(boxRect, ToolsSettings.GetLineColor(DefaultColors.BorderColor),
                ToolsSettings.GetBgColor(DefaultColors.BorderColorLight));

            var left = boxRect.x + DrawingUtility.Padding;
            var right = boxRect.xMax - DrawingUtility.Padding;

            // × button
            var btnRect = new Rect(right - DrawingUtility.ButtonWidth,
                boxRect.y + 3f, DrawingUtility.ButtonWidth, boxRect.height - 6f);
            var c = GUI.backgroundColor;
            GUI.backgroundColor = ToolsSettings.GetBgColor(DefaultColors.ButtonBg);
            if (GUI.Button(btnRect, "×", EditorStyles.miniButton))
                indexToRemove = index;
            GUI.backgroundColor = c;
            right = btnRect.x - DrawingUtility.Padding;

            // Drag handle (≡)
            var dragId = GUIUtility.GetControlID(FocusType.Passive);
            var handleW = DrawingUtility.ButtonWidth * 0.7f;
            var handleRect = new Rect(left, boxRect.y + DrawingUtility.Padding, handleW,
                boxRect.height - DrawingUtility.Padding * 2f);
            EditorGUIUtility.AddCursorRect(handleRect, MouseCursor.Pan);
            GUI.Label(handleRect, "≡", DrawingUtility.GetDragHandleStyle());
            left = handleRect.xMax + 2f;

            if (Event.current.type == EventType.MouseDown && Event.current.button == 0 &&
                handleRect.Contains(Event.current.mousePosition))
            {
                GUIUtility.hotControl = dragId;
                _isDragging = true;
                _dragPropertyPath = arrayProp.propertyPath;
                _dragSourceIndex = index;
                _dragDropTarget = -1;
                _dragControlId = dragId;
                Event.current.Use();
            }

            // Property field (inline, no label)
            var fieldRect = new Rect(left, boxRect.y + DrawingUtility.Padding,
                right - left, boxRect.height - DrawingUtility.Padding * 2f);
            EditorGUI.PropertyField(fieldRect, element, GUIContent.none);

            // ПКМ → контекстное меню
            if (Event.current.type == EventType.ContextClick && boxRect.Contains(Event.current.mousePosition))
            {
                ShowContextMenu(element, arrayProp, index, false);
                Event.current.Use();
            }
        }

        private static void DrawNormalElement(Rect boxRect, SerializedProperty element,
            SerializedProperty arrayProp, int index, bool isManagedRef, ref int indexToRemove)
        {
            var isDragSource = _isDragging && _dragPropertyPath == arrayProp.propertyPath && _dragSourceIndex == index;
            var rect = DrawItemHeaderBox(boxRect, index, ref indexToRemove, element.isExpanded, false, isDragSource);

            var color = ToolsSettings.GetLineColor(DefaultColors.BoxBg);
            color = DrawingUtility.ApplyOddCorrection(color, index);

            var left = rect.x + DrawingUtility.Padding;
            var right = rect.xMax - DrawingUtility.Padding;

            // Drag handle (≡)
            var dragId = GUIUtility.GetControlID(FocusType.Passive);
            var handleW = DrawingUtility.ButtonWidth * 0.7f;
            var handleRect = new Rect(left, rect.y + DrawingUtility.Padding, handleW,
                rect.height - DrawingUtility.Padding * 2f);
            EditorGUIUtility.AddCursorRect(handleRect, MouseCursor.Pan);
            GUI.Label(handleRect, "≡", DrawingUtility.GetDragHandleStyle());
            left = handleRect.xMax + 2f;

            // Drag start
            if (Event.current.type == EventType.MouseDown && Event.current.button == 0 &&
                handleRect.Contains(Event.current.mousePosition))
            {
                GUIUtility.hotControl = dragId;
                _isDragging = true;
                _dragPropertyPath = arrayProp.propertyPath;
                _dragSourceIndex = index;
                _dragDropTarget = -1;
                _dragControlId = dragId;
                Event.current.Use();
            }

            // Badge (index)
            var w = DrawingUtility.ButtonWidth * 1f;
            rect = new Rect(left, rect.y + DrawingUtility.Padding, w,
                rect.height - DrawingUtility.Padding * 2f);
            left = rect.xMax + DrawingUtility.Padding;

            EditorGUI.DrawRect(rect, ToolsSettings.GetBgColor(DefaultColors.BadgeBg));
            DrawingUtility.DrawBoxBorder(rect, ToolsSettings.GetLineColor(DefaultColors.BorderColor),
                ToolsSettings.GetBgColor(DefaultColors.BorderColorLight), false);
            GUI.Label(rect, index.ToString(), DrawingUtility.GetBadgeStyle());

            rect = new Rect(left, rect.y, right - left, rect.height);
            var label = "";

            var owner = InspectorHandler.GetValueOfProperty(element);
            if (owner != null &&
                Attribute.GetCustomAttribute(owner.GetType(), typeof(ClassLabelAttribute)) is ClassLabelAttribute attr)
                label = GetCustomLabel(owner, attr, index);
            else if (isManagedRef
                     && element.propertyType == SerializedPropertyType.ManagedReference
                     && element.managedReferenceValue != null)
                label = element.managedReferenceValue.GetType().Name;
            GUI.Label(rect, label, DrawingUtility.GetItemHeaderStyle());

            // ЛКМ → fold (не на drag handle)
            if (Event.current.type == EventType.MouseDown && Event.current.button == 0 &&
                rect.Contains(Event.current.mousePosition) && !_isDragging)
            {
                element.isExpanded = !element.isExpanded;
                Event.current.Use();
            }

            // ПКМ → контекстное меню
            if (Event.current.type == EventType.ContextClick && rect.Contains(Event.current.mousePosition))
            {
                ShowContextMenu(element, arrayProp, index, isManagedRef);
                Event.current.Use();
            }

            var borderRect = new Rect(boxRect.x, boxRect.y + DrawingUtility.RowHeight - 1f, boxRect.width,
                boxRect.height + 2f - DrawingUtility.RowHeight);
            /*
            DrawingUtility.DrawBoxBorder(borderRect, ToolsSettings.GetLineColor(DefaultColors.BorderColor),
                ToolsSettings.GetBgColor(DefaultColors.BorderColorLight));
            */
            if (!element.isExpanded)
                return;

            rect = new Rect(boxRect.x + 1f, boxRect.y + DrawingUtility.RowHeight, boxRect.width - 2f,
                boxRect.height - DrawingUtility.RowHeight);
            EditorGUI.DrawRect(rect, color);
            DrawingUtility.DrawBoxBorder(borderRect, ToolsSettings.GetLineColor(DefaultColors.ToggleBoxBorder),
                drawTop: false);
            DrawingUtility.DrawBoxBorder(rect, ToolsSettings.GetLineColor(DefaultColors.ToggleBoxBorder),
                drawTop: false, drawLeft: false, drawRight: false);
            rect = new Rect(rect.x + DrawingUtility.Padding * 4f, rect.y + DrawingUtility.Padding,
                rect.width - DrawingUtility.Padding * 5f, rect.height - DrawingUtility.Padding);
            DrawElementContent(rect, element);
        }

        private static void DrawNullElement(Rect boxRect, SerializedProperty element,
            SerializedProperty arrayProp, int index, FieldInfo fieldInfo, ref int indexToRemove)
        {
            var isDragSource = _isDragging && _dragPropertyPath == arrayProp.propertyPath && _dragSourceIndex == index;
            DrawItemHeaderBox(boxRect, index, ref indexToRemove, false, true, isDragSource);

            // Drag handle
            var dragId = GUIUtility.GetControlID(FocusType.Passive);
            var handleW = DrawingUtility.ButtonWidth * 0.7f;
            var handleRect = new Rect(boxRect.x + DrawingUtility.Padding, boxRect.y + DrawingUtility.Padding,
                handleW, DrawingUtility.RowHeight - DrawingUtility.Padding * 2f);
            EditorGUIUtility.AddCursorRect(handleRect, MouseCursor.Pan);
            GUI.Label(handleRect, "≡", DrawingUtility.GetDragHandleStyle());

            if (Event.current.type == EventType.MouseDown && Event.current.button == 0 &&
                handleRect.Contains(Event.current.mousePosition))
            {
                GUIUtility.hotControl = dragId;
                _isDragging = true;
                _dragPropertyPath = arrayProp.propertyPath;
                _dragSourceIndex = index;
                _dragDropTarget = -1;
                _dragControlId = dragId;
                Event.current.Use();
            }

            var handleOffset = handleW + 2f;
            var rect = new Rect(boxRect.x + DrawingUtility.Padding + handleOffset, boxRect.y + DrawingUtility.Padding,
                boxRect.width - 2f * DrawingUtility.Padding - DrawingUtility.ButtonWidth - handleOffset,
                DrawingUtility.RowHeight - 2f * DrawingUtility.Padding);

            var baseType = TypeResolver.GetElementBaseType(fieldInfo);
            if (baseType == null) return;

            var types = TypeResolver.GetAssignableTypes(baseType);
            if (types.Count == 0)
            {
                EditorGUI.LabelField(rect, $"There isn't items for this type: {baseType.Name}",
                    EditorStyles.helpBox);
                return;
            }

            var names = new string[types.Count];
            for (var j = 0; j < types.Count; j++)
                names[j] = TypeResolver.FormatTypeName(types[j].Name);

            var controlId = (arrayProp.propertyPath + "_null_" + index).GetHashCode();
            var selected = SearchablePopup.Draw(rect, controlId, -1, names, "— Choose Type —");

            if (selected >= 0 && selected < types.Count)
            {
                var selectedType = types[selected];
                var propPath = arrayProp.propertyPath;
                var elemIdx = index;
                var so = arrayProp.serializedObject;

                EditorApplication.delayCall += () =>
                {
                    if (so == null || so.targetObject == null) return;
                    so.Update();
                    var freshProp = so.FindProperty(propPath);
                    if (freshProp == null || !freshProp.isArray || elemIdx >= freshProp.arraySize) return;
                    var freshElem = freshProp.GetArrayElementAtIndex(elemIdx);
                    freshElem.managedReferenceValue = Activator.CreateInstance(selectedType);
                    freshElem.isExpanded = true;
                    so.ApplyModifiedProperties();
                };
            }
        }

        private static Rect DrawItemHeaderBox(Rect boxRect, int index, ref int indexToRemove, bool expand,
            bool error = false, bool isDragSource = false)
        {
            var rect = new Rect(boxRect.x, boxRect.y, boxRect.width, DrawingUtility.RowHeight);

            Color color;
            if (isDragSource)
                color = ToolsSettings.GetBgColor(DefaultColors.DragIndicator);
            else if (error)
                color = ToolsSettings.GetBgColor(DefaultColors.ErrorBg);
            else
                color = ToolsSettings.GetBgColor(expand ? DefaultColors.HeaderBg : DefaultColors.HeaderBgCollapsed);
            color = DrawingUtility.ApplyOddCorrection(color, index);

            EditorGUI.DrawRect(rect, color);
            DrawingUtility.DrawBoxBorder(rect, ToolsSettings.GetLineColor(DefaultColors.BorderColor),
                ToolsSettings.GetBgColor(DefaultColors.BorderColorLight));

            rect = new Rect(rect.xMax - DrawingUtility.ButtonWidth - DrawingUtility.Padding, rect.y + 3f,
                DrawingUtility.ButtonWidth, DrawingUtility.RowHeight - 6f);

            var c = GUI.backgroundColor;
            GUI.backgroundColor = ToolsSettings.GetBgColor(DefaultColors.ButtonBg);
            if (GUI.Button(rect, "×", EditorStyles.miniButton))
                indexToRemove = index;
            GUI.backgroundColor = c;

            return new Rect(boxRect.x, boxRect.y, rect.x - boxRect.x, DrawingUtility.RowHeight);
        }

        // ════════════════════════════════════════════════════════
        //  Шапка коллекции
        // ════════════════════════════════════════════════════════

        private static bool DrawCollectionHeader(Rect propertyRect, SerializedProperty property, GUIContent content,
            bool isManagedRef)
        {
            propertyRect = new Rect(propertyRect.x, propertyRect.y, propertyRect.width, DrawingUtility.RowHeight);
            EditorGUI.DrawRect(propertyRect, ToolsSettings.GetBgColor(DefaultColors.HeaderBg));
            DrawingUtility.DrawBoxBorder(propertyRect, ToolsSettings.GetLineColor(DefaultColors.BorderColor),
                ToolsSettings.GetBgColor(DefaultColors.BorderColorLight));
            var rect = new Rect(propertyRect.x, propertyRect.y, DrawingUtility.ButtonWidth, DrawingUtility.RowHeight);
            var leftBorder = rect.xMax;
            GUI.Label(rect, property.isExpanded ? "▼" : "▶", new GUIStyle(EditorStyles.miniLabel)
            {
                normal = { textColor = ToolsSettings.GetLineColor(DefaultColors.TextColorInactive) },
                alignment = TextAnchor.MiddleCenter, fontSize = 8
            });

            var w = DrawingUtility.ButtonWidth * 2f;
            rect = new Rect(propertyRect.xMax - w - DrawingUtility.Padding,
                propertyRect.y + DrawingUtility.Padding - 1f, w, DrawingUtility.InnerHeight);
            var rightBorder = rect.x;

            var originalBg = GUI.backgroundColor;
            GUI.backgroundColor = ToolsSettings.GetBgColor(DefaultColors.ButtonBg);
            if (GUI.Button(rect, "Add", EditorStyles.miniButton))
            {
                var propPath = property.propertyPath;
                var so = property.serializedObject;
                var useManagedRef = isManagedRef;

                EditorApplication.delayCall += () =>
                {
                    if (so == null || so.targetObject == null) return;
                    so.Update();
                    var freshProp = so.FindProperty(propPath);
                    if (freshProp == null || !freshProp.isArray) return;
                    freshProp.arraySize++;
                    so.ApplyModifiedProperties();
                    so.Update();
                    var ne = freshProp.GetArrayElementAtIndex(freshProp.arraySize - 1);
                    if (useManagedRef)
                    {
                        ne.managedReferenceValue = null;
                        ne.isExpanded = false;
                    }
                    else
                    {
                        ne.isExpanded = true;
                    }

                    so.ApplyModifiedProperties();
                };
            }

            GUI.backgroundColor = originalBg;

            w = DrawingUtility.ButtonWidth;
            rect = new Rect(rightBorder - w - DrawingUtility.Padding, propertyRect.y + DrawingUtility.Padding,
                w, DrawingUtility.InnerHeight);
            rightBorder = rect.x;

            EditorGUI.DrawRect(rect, ToolsSettings.GetBgColor(DefaultColors.BadgeBg));
            DrawingUtility.DrawBoxBorder(rect, ToolsSettings.GetLineColor(DefaultColors.BorderColor),
                ToolsSettings.GetBgColor(DefaultColors.BorderColorLight), false);
            GUI.Label(rect, property.arraySize.ToString(), DrawingUtility.GetBadgeStyle());

            w = rightBorder - leftBorder - DrawingUtility.Padding * 2f;
            rect = new Rect(leftBorder + DrawingUtility.Padding, propertyRect.y + DrawingUtility.Padding,
                w, DrawingUtility.InnerHeight);
            GUI.Label(rect, content, DrawingUtility.GetHeaderStyle());

            // ПКМ → контекстное меню
            if (Event.current.type == EventType.ContextClick && propertyRect.Contains(Event.current.mousePosition))
            {
                ShowCollectionContextMenu(property, isManagedRef);
                Event.current.Use();
            }

            // ЛКМ → fold
            if (Event.current.type == EventType.MouseDown && Event.current.button == 0 &&
                propertyRect.Contains(Event.current.mousePosition))
            {
                property.isExpanded = !property.isExpanded;
                Event.current.Use();
            }

            return property.isExpanded;
        }

        // ════════════════════════════════════════════════════════
        //  Контекстные меню
        // ════════════════════════════════════════════════════════

        private static void ShowCollectionContextMenu(SerializedProperty collection, bool isManagedRef)
        {
            var menu = new GenericMenu();
            var propertyPath = collection.propertyPath;
            var serializedObj = collection.serializedObject;

            menu.AddItem(new GUIContent(collection.isExpanded ? "Collapse" : "Expand"), false,
                () =>
                {
                    var freshCollection = serializedObj.FindProperty(propertyPath);
                    freshCollection.isExpanded = !freshCollection.isExpanded;
                });

            menu.AddSeparator("");

            menu.AddItem(new GUIContent("Expand All Items"), false, () =>
            {
                var freshCollection = serializedObj.FindProperty(propertyPath);
                SetAllItemsExpanded(freshCollection, true);
            });

            menu.AddItem(new GUIContent("Collapse All Items"), false, () =>
            {
                var freshCollection = serializedObj.FindProperty(propertyPath);
                SetAllItemsExpanded(freshCollection, false);
            });

            menu.AddSeparator("");

            menu.AddItem(new GUIContent("Clear"), false, () =>
            {
                var freshCollection = serializedObj.FindProperty(propertyPath);
                freshCollection.ClearArray();
                freshCollection.serializedObject.ApplyModifiedProperties();
            });

            menu.AddSeparator("");

            menu.AddItem(new GUIContent("Copy"), false, () =>
            {
                var freshCollection = serializedObj.FindProperty(propertyPath);
                EditorGUIUtility.systemCopyBuffer = PropertyClipboard.Serialize(freshCollection);
            });

            if (!string.IsNullOrEmpty(EditorGUIUtility.systemCopyBuffer))
                menu.AddItem(new GUIContent("Paste"), false, () =>
                {
                    var freshCollection = serializedObj.FindProperty(propertyPath);
                    PropertyClipboard.Deserialize(freshCollection, EditorGUIUtility.systemCopyBuffer);
                    serializedObj.ApplyModifiedProperties();
                });
            else
                menu.AddDisabledItem(new GUIContent("Paste"));

            menu.AddItem(new GUIContent("Copy Property Path"), false,
                () => EditorGUIUtility.systemCopyBuffer = propertyPath);

            menu.ShowAsContext();
        }

        private static void ShowContextMenu(SerializedProperty element, SerializedProperty collection,
            int index, bool isManagedRef)
        {
            var menu = new GenericMenu();
            var propertyPath = collection.propertyPath;
            var serializedObj = collection.serializedObject;

            if (isManagedRef)
            {
                menu.AddItem(new GUIContent("Set Null"), false, () =>
                {
                    serializedObj.Update();
                    var freshCollection = serializedObj.FindProperty(propertyPath);
                    if (freshCollection != null && index < freshCollection.arraySize)
                    {
                        var freshElement = freshCollection.GetArrayElementAtIndex(index);
                        if (freshElement.propertyType == SerializedPropertyType.ManagedReference)
                        {
                            freshElement.managedReferenceValue = null;
                            freshElement.isExpanded = false;
                        }

                        serializedObj.ApplyModifiedProperties();
                    }
                });
                menu.AddSeparator("");
            }

            menu.AddItem(new GUIContent(element.isExpanded ? "Collapse" : "Expand"), false,
                () =>
                {
                    serializedObj.Update();
                    var freshCollection = serializedObj.FindProperty(propertyPath);
                    if (freshCollection != null && index < freshCollection.arraySize)
                    {
                        var freshElement = freshCollection.GetArrayElementAtIndex(index);
                        freshElement.isExpanded = !freshElement.isExpanded;
                    }
                });

            menu.AddSeparator("");

            if (index > 0)
                menu.AddItem(new GUIContent("↑ Move Up"), false, () =>
                {
                    var freshCollection = serializedObj.FindProperty(propertyPath);
                    freshCollection.MoveArrayElement(index, index - 1);
                    freshCollection.serializedObject.ApplyModifiedProperties();
                });

            if (index < collection.arraySize - 1)
                menu.AddItem(new GUIContent("↓ Move Down"), false, () =>
                {
                    var freshCollection = serializedObj.FindProperty(propertyPath);
                    freshCollection.MoveArrayElement(index, index + 1);
                    freshCollection.serializedObject.ApplyModifiedProperties();
                });

            menu.AddSeparator("");

            menu.AddItem(new GUIContent("Duplicate"), false, () =>
            {
                var freshCollection = serializedObj.FindProperty(propertyPath);
                freshCollection.InsertArrayElementAtIndex(index);
                serializedObj.ApplyModifiedProperties();
            });

            menu.AddItem(new GUIContent("Remove"), false, () =>
            {
                var freshCollection = serializedObj.FindProperty(propertyPath);
                freshCollection.DeleteArrayElementAtIndex(index);
                freshCollection.serializedObject.ApplyModifiedProperties();
            });

            menu.AddSeparator("");

            var elementPath = element.propertyPath;

            menu.AddItem(new GUIContent("Copy"), false, () =>
            {
                var freshElement = serializedObj.FindProperty(elementPath);
                EditorGUIUtility.systemCopyBuffer = PropertyClipboard.Serialize(freshElement);
            });

            if (!string.IsNullOrEmpty(EditorGUIUtility.systemCopyBuffer))
                menu.AddItem(new GUIContent("Paste"), false, () =>
                {
                    var freshElement = serializedObj.FindProperty(elementPath);
                    PropertyClipboard.Deserialize(freshElement, EditorGUIUtility.systemCopyBuffer);
                    serializedObj.ApplyModifiedProperties();
                });
            else
                menu.AddDisabledItem(new GUIContent("Paste"));

            menu.AddItem(new GUIContent("Copy Property Path"), false,
                () => EditorGUIUtility.systemCopyBuffer = elementPath);

            menu.ShowAsContext();
        }

        // ════════════════════════════════════════════════════════
        //  Drag & Drop
        // ════════════════════════════════════════════════════════

        private static void HandleDragDrop(Rect position, SerializedProperty property, int arraySize)
        {
            var ev = Event.current;
            var mouseY = ev.mousePosition.y;
            var indicatorColor = ToolsSettings.GetLineColor(DefaultColors.DragIndicator);

            // Определяем drop target по позиции мыши
            _dragDropTarget = arraySize; // по умолчанию — в конец
            for (int i = 0; i < arraySize; i++)
            {
                var midY = _elementYPositions[i] + (_elementYPositions[i + 1] - _elementYPositions[i]
                                                                              - DrawingUtility.ElementSpacing) * 0.5f;
                if (mouseY < midY)
                {
                    _dragDropTarget = i;
                    break;
                }
            }

            // Рисуем индикатор
            if (_dragDropTarget != _dragSourceIndex && _dragDropTarget != _dragSourceIndex + 1)
            {
                float indicatorY;
                if (_dragDropTarget < arraySize)
                    indicatorY = _elementYPositions[_dragDropTarget] - DrawingUtility.ElementSpacing * 0.5f;
                else
                    indicatorY = _elementYPositions[arraySize] - DrawingUtility.ElementSpacing * 0.5f;

                var indicatorRect = new Rect(position.x + 4f, indicatorY - 1f, position.width - 8f, 2f);
                EditorGUI.DrawRect(indicatorRect, indicatorColor);

                // Стрелочки по краям
                EditorGUI.DrawRect(new Rect(indicatorRect.x, indicatorY - 3f, 6f, 6f), indicatorColor);
                EditorGUI.DrawRect(new Rect(indicatorRect.xMax - 6f, indicatorY - 3f, 6f, 6f), indicatorColor);
            }

            // Mouse up → выполняем перемещение
            if (ev.type == EventType.MouseUp && ev.button == 0
                                             && GUIUtility.hotControl == _dragControlId)
            {
                if (_dragDropTarget >= 0 && _dragDropTarget != _dragSourceIndex
                                         && _dragDropTarget != _dragSourceIndex + 1)
                {
                    var targetIndex = _dragDropTarget > _dragSourceIndex
                        ? _dragDropTarget - 1
                        : _dragDropTarget;

                    var srcIdx = _dragSourceIndex;
                    var moveCallback = OnMoveElement;

                    if (moveCallback != null)
                    {
                        // Odin path: callback обработает перемещение своим способом
                        moveCallback(property, srcIdx, targetIndex);
                    }
                    else
                    {
                        // Native path: стандартный MoveArrayElement через delayCall
                        var propPath = property.propertyPath;
                        var so = property.serializedObject;
                        EditorApplication.delayCall += () =>
                        {
                            if (so == null || so.targetObject == null) return;
                            so.Update();
                            var freshProp = so.FindProperty(propPath);
                            if (freshProp == null || !freshProp.isArray) return;
                            if (srcIdx >= freshProp.arraySize || targetIndex >= freshProp.arraySize) return;
                            freshProp.MoveArrayElement(srcIdx, targetIndex);
                            so.ApplyModifiedProperties();
                        };
                    }

                    ResetDragState();
                    ev.Use();
                    return;
                }

                ResetDragState();
                ev.Use();
            }

            // Mouse drag → repaint
            if (ev.type == EventType.MouseDrag && GUIUtility.hotControl == _dragControlId)
            {
                ev.Use();
            }

            // Escape → отмена
            if (ev.type == EventType.KeyDown && ev.keyCode == KeyCode.Escape)
            {
                ResetDragState();
                ev.Use();
            }

            // Запрашиваем перерисовку при активном drag
            if (_isDragging)
            {
                HandleUtility.Repaint();
            }
        }

        // ════════════════════════════════════════════════════════
        //  Утилиты
        // ════════════════════════════════════════════════════════

        private static void SetAllItemsExpanded(SerializedProperty collection, bool expanded)
        {
            for (int i = 0; i < collection.arraySize; i++)
                collection.GetArrayElementAtIndex(i).isExpanded = expanded;
        }

        private static float GetElementContentHeight(SerializedProperty element)
        {
            if (!element.isExpanded)
                return DrawingUtility.RowHeight;
            var height = 0f;
            var iter = element.Copy();
            var end = iter.GetEndProperty();
            var enter = true;
            while (iter.NextVisible(enter))
            {
                enter = false;
                if (SerializedProperty.EqualContents(iter, end)) break;
                height += EditorGUI.GetPropertyHeight(iter, true) + EditorGUIUtility.standardVerticalSpacing;
            }

            return DrawingUtility.RowHeight + Mathf.Max(height, 0f) + DrawingUtility.Padding * 2f;
        }

        private static string GetCustomLabel(object owner, ClassLabelAttribute classLabelAttr, int index)
        {
            var result = ReflectionHelper.ResolveTextOrMethodOnOwner(owner, classLabelAttr.GroupName, index);
            return result ?? classLabelAttr.GroupName;
        }

        private static void DrawElementContent(Rect rect, SerializedProperty element)
        {
            var iter = element.Copy();
            var end = iter.GetEndProperty();
            bool enter = true;
            float y = rect.y;
            while (iter.NextVisible(enter))
            {
                enter = false;
                if (SerializedProperty.EqualContents(iter, end)) break;
                var h = EditorGUI.GetPropertyHeight(iter, true);
                EditorGUI.PropertyField(
                    new Rect(rect.x + DrawingUtility.Padding, y, rect.width - 2 * DrawingUtility.Padding, h), iter,
                    true);
                y += h + EditorGUIUtility.standardVerticalSpacing;
            }
        }

        /// <summary>
        /// Перемещает элемент массива/списка напрямую через reflection,
        /// минуя SerializedProperty.MoveArrayElement.
        /// </summary>
        internal static void MoveElementDirect(SerializedObject so, string propertyPath, int srcIndex, int dstIndex)
        {
            var target = so.targetObject;
            if (target == null) return;

            Undo.RecordObject(target, "Reorder Collection");

            object owner = target;
            FieldInfo fieldInfo = null;
            var segments = propertyPath.Split('.');
            var type = target.GetType();
            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

            for (var i = 0; i < segments.Length; i++)
            {
                var segment = segments[i];
                if (segment == "Array") break;

                // Кешированный поиск с обходом базовых классов
                fieldInfo = ReflectionCache.GetFieldWithBase(type, segment, flags);
                if (fieldInfo == null) return;

                if (i < segments.Length - 1 && segments[i + 1] != "Array")
                {
                    owner = fieldInfo.GetValue(owner);
                    if (owner == null) return;
                    type = owner.GetType();
                }
            }

            if (fieldInfo == null) return;
            var value = fieldInfo.GetValue(owner);

            if (value is Array arr)
            {
                var element = arr.GetValue(srcIndex);
                if (srcIndex < dstIndex)
                    for (var i = srcIndex; i < dstIndex; i++)
                        arr.SetValue(arr.GetValue(i + 1), i);
                else
                    for (var i = srcIndex; i > dstIndex; i--)
                        arr.SetValue(arr.GetValue(i - 1), i);
                arr.SetValue(element, dstIndex);
            }
            else if (value is IList list)
            {
                var element = list[srcIndex];
                list.RemoveAt(srcIndex);
                list.Insert(dstIndex, element);
            }

            EditorUtility.SetDirty(target);
            so.Update();
        }
    }
}
#endif