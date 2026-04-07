#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using Vortex.Unity.EditorTools.Abstraction;
using Vortex.Unity.EditorTools.AttributeDrawers;
using Vortex.Unity.EditorTools.Elements;
using Vortex.Unity.FormulaEvaluatorSystem.Attributes;

namespace Vortex.Unity.FormulaEvaluatorSystem
{
    [CustomPropertyDrawer(typeof(FormulaAttribute))]
    public class FormulaDrawer : MultiDrawer
    {
        private const float SlotLabelWidth = 30f;
        private const float TestFieldWidth = 60f;

        public override void PreRender(PropertyData data, PropertyAttribute attribute)
        {
            data.IsCustomField();
            data.IsCustomLabel();

            var slotCount = GetSlotCount(data);
            // label row + formula row + slot rows
            var totalHeight = DrawingUtility.RowHeight * (2 + slotCount);
            var extra = totalHeight - data.BaseHeight;
            data.BaseHeight = totalHeight;
            data.AddHeight(extra);
        }

        public override float RenderTopper(PropertyData data, PropertyAttribute attribute, bool onlyCalculation)
        {
            var attr = (FormulaAttribute)attribute;
            var formula = data.Property.stringValue;
            if (string.IsNullOrEmpty(formula)) return 0;

            var error = ValidateCompanionField(data, attr);
            if (error != null)
            {
                var errHeight = DrawingUtility.CalcInfoBoxHeight(error, data.Width);
                if (!onlyCalculation)
                    DrawingUtility.MakeInfoBox(data.Position, error, true);
                return errHeight;
            }

            var slotCount = FormulaParser.GetMaxSlotIndex(formula) + 1;
            var parameters = new double[slotCount];
            var slotsProperty = FindSlotsProperty(data, attr);

            if (slotsProperty != null)
            {
                for (int i = 0; i < slotCount && i < slotsProperty.arraySize; i++)
                {
                    var element = slotsProperty.GetArrayElementAtIndex(i);
                    var testProp = element.FindPropertyRelative("testValue");
                    if (testProp != null)
                        parameters[i] = testProp.floatValue;
                }
            }

            string text;
            bool hasError;
            if (FormulaParser.TryEvaluate(formula, parameters, out var result, out var evalError))
            {
                text = $"Result: {result:G6}";
                hasError = false;
            }
            else
            {
                text = $"Error: {evalError}";
                hasError = true;
            }

            var height = DrawingUtility.CalcInfoBoxHeight(text, data.Width);
            if (!onlyCalculation)
                DrawingUtility.MakeInfoBox(data.Position, text, hasError);
            return height;
        }

        public override void RenderLabel(PropertyData data, PropertyAttribute attribute)
        {
            var labelRect = new Rect(data.Position.x, data.Position.y,
                data.Position.width, EditorGUIUtility.singleLineHeight);
            EditorGUI.LabelField(labelRect, data.Label, EditorStyles.boldLabel);

            var pos = data.Position;
            pos.y += DrawingUtility.RowHeight;
            data.Position = pos;
        }

        public override void RenderField(PropertyData data, PropertyAttribute attribute)
        {
            var attr = (FormulaAttribute)attribute;
            var pos = data.Position;

            // Formula text field
            var formulaRect = new Rect(pos.x, pos.y, pos.width, EditorGUIUtility.singleLineHeight);
            EditorGUI.BeginChangeCheck();
            var newFormula = EditorGUI.TextField(formulaRect, data.Property.stringValue);
            if (EditorGUI.EndChangeCheck())
            {
                data.Property.stringValue = newFormula;
                data.Property.serializedObject.ApplyModifiedProperties();
            }

            pos.y += DrawingUtility.RowHeight;

            // Slots
            var slotsProperty = FindSlotsProperty(data, attr);
            if (slotsProperty == null) return;

            var slotCount = FormulaParser.GetMaxSlotIndex(data.Property.stringValue) + 1;
            if (slotsProperty.arraySize != slotCount)
            {
                slotsProperty.arraySize = slotCount;
                slotsProperty.serializedObject.ApplyModifiedProperties();
            }

            var owner = data.Owner ?? data.Property.serializedObject.targetObject;
            if (owner == null) return;

            var resolved = FormulaReflectionResolver.Resolve(owner.GetType());

            for (int i = 0; i < slotCount; i++)
            {
                var element = slotsProperty.GetArrayElementAtIndex(i);
                var memberNameProp = element.FindPropertyRelative("memberName");
                var testValueProp = element.FindPropertyRelative("testValue");

                var slotRect = new Rect(pos.x, pos.y, pos.width, EditorGUIUtility.singleLineHeight);

                // Slot label {N}
                var labelRect = new Rect(slotRect.x, slotRect.y, SlotLabelWidth, slotRect.height);
                EditorGUI.LabelField(labelRect, $"{{{i}}}");

                // Popup selector
                var popupWidth = slotRect.width - SlotLabelWidth - TestFieldWidth - DrawingUtility.Padding * 2;
                var popupRect = new Rect(slotRect.x + SlotLabelWidth + DrawingUtility.Padding,
                    slotRect.y, popupWidth, slotRect.height);

                var currentName = memberNameProp.stringValue;
                var currentIndex = FormulaReflectionResolver.FindMemberIndex(resolved.MemberNames, currentName);

                DrawingUtility.DrawSelector(popupRect, memberNameProp,
                    resolved.PopupKeys, resolved.MemberNames as object[], currentIndex, $"{{{i}}}");

                // Test value
                var testRect = new Rect(slotRect.x + slotRect.width - TestFieldWidth,
                    slotRect.y, TestFieldWidth, slotRect.height);
                EditorGUI.BeginChangeCheck();
                var newTestValue = EditorGUI.FloatField(testRect, testValueProp.floatValue);
                if (EditorGUI.EndChangeCheck())
                {
                    testValueProp.floatValue = newTestValue;
                    testValueProp.serializedObject.ApplyModifiedProperties();
                }

                pos.y += DrawingUtility.RowHeight;
            }
        }

        private static int GetSlotCount(PropertyData data)
        {
            var formula = data.Property.stringValue;
            return string.IsNullOrEmpty(formula) ? 0 : FormulaParser.GetMaxSlotIndex(formula) + 1;
        }

        private static SerializedProperty FindSlotsProperty(PropertyData data, FormulaAttribute attr)
        {
            return data.Property.serializedObject.FindProperty(attr.SlotsFieldName);
        }

        private static string ValidateCompanionField(PropertyData data, FormulaAttribute attr)
        {
            var slotsProp = FindSlotsProperty(data, attr);
            if (slotsProp == null)
                return $"Field '{attr.SlotsFieldName}' not found";
            if (!slotsProp.isArray)
                return $"Field '{attr.SlotsFieldName}' must be FormulaSlot[]";
            return null;
        }
    }
}
#endif
