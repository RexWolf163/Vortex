#if UNITY_EDITOR && ODIN_INSPECTOR
using Sirenix.OdinInspector.Editor;
using Sirenix.Utilities.Editor;
using UnityEditor;
using UnityEngine;
using Vortex.Unity.EditorTools.Elements;
using Vortex.Unity.FormulaEvaluatorSystem.Attributes;

namespace Vortex.Unity.FormulaEvaluatorSystem
{
    /// <summary>
    /// Odin-drawer для <see cref="FormulaAttribute"/>.
    /// Рисует поле формулы со списком слотов и блоком результата/ошибки.
    /// </summary>
    public sealed class FormulaDrawer : OdinAttributeDrawer<FormulaAttribute, string>
    {
        private const float SlotLabelWidth = 30f;
        private const float TestFieldWidth = 60f;
        private static readonly float RowHeight = EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

        protected override void DrawPropertyLayout(GUIContent label)
        {
            var attr = Attribute;
            var unityProp = Property.Tree.UnitySerializedObject?.FindProperty(Property.UnityPropertyPath);
            if (unityProp == null)
            {
                CallNextDrawer(label);
                return;
            }

            var slotsProp = unityProp.serializedObject.FindProperty(attr.SlotsFieldName);
            if (slotsProp == null)
            {
                SirenixEditorGUI.ErrorMessageBox($"Field '{attr.SlotsFieldName}' not found");
                return;
            }

            if (!slotsProp.isArray)
            {
                SirenixEditorGUI.ErrorMessageBox($"Field '{attr.SlotsFieldName}' must be FormulaSlot[]");
                return;
            }

            DrawTopperBlock(unityProp, attr);

            if (label != null)
                EditorGUILayout.LabelField(label, EditorStyles.boldLabel);

            DrawFormulaField(unityProp);
            DrawSlots(unityProp, slotsProp);
        }

        // ════════════════════════════════════════════════════════
        //  Топпер: результат вычисления или ошибка
        // ════════════════════════════════════════════════════════

        private static void DrawTopperBlock(SerializedProperty property, FormulaAttribute attr)
        {
            var formula = property.stringValue;
            if (string.IsNullOrEmpty(formula)) return;

            var slotsProp = property.serializedObject.FindProperty(attr.SlotsFieldName);
            var slotCount = FormulaParser.GetMaxSlotIndex(formula) + 1;
            var parameters = new double[slotCount];

            if (slotsProp != null)
            {
                for (int i = 0; i < slotCount && i < slotsProp.arraySize; i++)
                {
                    var element = slotsProp.GetArrayElementAtIndex(i);
                    var testProp = element.FindPropertyRelative("testValue");
                    if (testProp != null)
                        parameters[i] = testProp.floatValue;
                }
            }

            if (FormulaParser.TryEvaluate(formula, parameters, out var result, out var evalError))
                SirenixEditorGUI.InfoMessageBox($"Result: {result:G6}");
            else
                SirenixEditorGUI.ErrorMessageBox($"Error: {evalError}");
        }

        // ════════════════════════════════════════════════════════
        //  Поле формулы
        // ════════════════════════════════════════════════════════

        private void DrawFormulaField(SerializedProperty property)
        {
            EditorGUI.BeginChangeCheck();
            var newFormula = EditorGUILayout.TextField(property.stringValue);
            if (EditorGUI.EndChangeCheck())
            {
                property.stringValue = newFormula;
                property.serializedObject.ApplyModifiedProperties();
            }
        }

        // ════════════════════════════════════════════════════════
        //  Слоты
        // ════════════════════════════════════════════════════════

        private void DrawSlots(SerializedProperty formulaProp, SerializedProperty slotsProperty)
        {
            var slotCount = FormulaParser.GetMaxSlotIndex(formulaProp.stringValue) + 1;
            if (slotsProperty.arraySize != slotCount)
            {
                slotsProperty.arraySize = slotCount;
                slotsProperty.serializedObject.ApplyModifiedProperties();
            }

            var owner = Property.ParentValues.Count > 0 ? Property.ParentValues[0] : null;
            owner ??= formulaProp.serializedObject.targetObject;
            if (owner == null) return;

            var resolved = FormulaReflectionResolver.Resolve(owner.GetType());

            for (int i = 0; i < slotCount; i++)
            {
                var element = slotsProperty.GetArrayElementAtIndex(i);
                var memberNameProp = element.FindPropertyRelative("memberName");
                var testValueProp = element.FindPropertyRelative("testValue");

                var slotRect = EditorGUILayout.GetControlRect();

                var labelRect = new Rect(slotRect.x, slotRect.y, SlotLabelWidth, slotRect.height);
                EditorGUI.LabelField(labelRect, $"{{{i}}}");

                var popupWidth = slotRect.width - SlotLabelWidth - TestFieldWidth - 4f;
                var popupRect = new Rect(slotRect.x + SlotLabelWidth + 2f, slotRect.y, popupWidth, slotRect.height);

                var currentName = memberNameProp.stringValue;
                var currentIndex = FormulaReflectionResolver.FindMemberIndex(resolved.MemberNames, currentName);

                DrawingUtility.DrawSelector(popupRect, memberNameProp,
                    resolved.PopupKeys, resolved.MemberNames as object[], currentIndex, $"{{{i}}}");

                var testRect = new Rect(slotRect.x + slotRect.width - TestFieldWidth, slotRect.y,
                    TestFieldWidth, slotRect.height);
                EditorGUI.BeginChangeCheck();
                var newTestValue = EditorGUI.FloatField(testRect, testValueProp.floatValue);
                if (EditorGUI.EndChangeCheck())
                {
                    testValueProp.floatValue = newTestValue;
                    testValueProp.serializedObject.ApplyModifiedProperties();
                }
            }
        }
    }
}
#endif
