using System;
using UnityEngine;

namespace Vortex.Unity.UI.Attributes
{
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = true)]
    public class StateSwitcherAttribute : PropertyAttribute
    {
        /// <summary>
        /// Все определённые хинты
        /// </summary>
        public readonly StateDesc[] States;

        /// <summary>
        /// Добавляет все значения Enum-а как хинты.
        /// Описание значения можно заменить добавлением атрибута Tooltip/LabelText на значения enum-а
        /// </summary>
        public StateSwitcherAttribute(Type enumType)
        {
            if (!enumType.IsEnum)
                throw new ArgumentException($"[StateSwitcher] {enumType.Name} is not an Enum type.");

            Array values = Enum.GetValues(enumType);
            States = new StateDesc[values.Length];

            for (int i = 0; i < States.Length; i++)
                States[i] = new StateDesc(values.GetValue(i));
        }

        [Serializable]
        public class StateDesc
        {
            public readonly int State;
            public readonly string Description;

            public StateDesc(int state, string description)
            {
                State = state;
                Description = description;
            }

            public StateDesc(object enumVal)
            {
                try
                {
                    State = Convert.ToInt32(enumVal);
                }
                catch
                {
                    State = -1;
                }

                Description = ResolveDescription(enumVal);
            }

            private static string ResolveDescription(object value)
            {
                string defVal = value.ToString();

                try
                {
                    var enumType = value.GetType();
                    var memberInfos = enumType.GetMember(defVal);
                    var enumMember = Array.Find(memberInfos, m => m.DeclaringType == enumType);

                    if (enumMember == null)
                        return defVal;

                    foreach (var attribute in enumMember.GetCustomAttributes(false))
                    {
                        switch (attribute)
                        {
                            case TooltipAttribute tip:
                                return tip.tooltip;
                        }

                        // Sirenix/Odin атрибуты — проверяем по имени типа,
                        // чтобы не создавать жёсткую зависимость
                        var attrType = attribute.GetType();

                        if (attrType.Name == "LabelTextAttribute")
                        {
                            var textProp = attrType.GetProperty("Text")
                                           ?? attrType.GetProperty("text");
                            if (textProp != null)
                                return textProp.GetValue(attribute) as string ?? defVal;
                        }

                        if (attrType.Name == "PropertyTooltipAttribute")
                        {
                            var tooltipProp = attrType.GetProperty("Tooltip")
                                              ?? attrType.GetProperty("tooltip");
                            if (tooltipProp != null)
                                return tooltipProp.GetValue(attribute) as string ?? defVal;
                        }
                    }

                    return defVal;
                }
                catch
                {
                    return defVal;
                }
            }
        }
    }
    /*
    public class StateSwitcherAttribute : Attribute
    {
        /// <summary>
        /// Все определенные хинты
        /// </summary>
        public readonly StateDesc[] States;

        /// <summary>
        /// Добавляет все значения Enum-а как хинты.
        /// Описание значения можно заменить добавлением аттрибута Tooltip/LabelText на значения enum-а
        /// </summary>
        public StateSwitcherAttribute(Type _enumType)
        {
            Array values = Enum.GetValues(_enumType);

            States = new StateDesc[values.Length];
            for (int i = States.Length - 1; i >= 0; i--)
                States[i] = new StateDesc(values.GetValue(i));
        }

        public class StateDesc
        {
            public readonly int State;
            public readonly string Description;

            public StateDesc(int state, string _description)
            {
                State = state;
                Description = _description;
            }

            public StateDesc(object enumVal)
            {
                try
                {
                    State = (int)enumVal;
                }
                catch (Exception ex)
                {
                    State = -1;
                }

                Description = GetDescription(enumVal);
            }

            private static string GetDescription(object value)
            {
                string defVal = value.ToString();

                try
                {
                    var enumType = value.GetType();
                    var memberInfos = enumType.GetMember(defVal);
                    var enumValueMemberInfo = Array.Find(memberInfos, m => m.DeclaringType == enumType);
                    foreach (object attribute in enumValueMemberInfo.GetCustomAttributes(false))
                    {
                        switch (attribute)
                        {
                            case PropertyTooltipAttribute pTipAttr:
                                return pTipAttr.Tooltip;
                            case LabelTextAttribute label:
                                return label.Text;
                            case TooltipAttribute tipAttr:
                                return tipAttr.tooltip;
                        }
                    }

                    return defVal;
                }
                catch
                {
                    return defVal;
                }
            }
        }
    }
    */

/*
#if UNITY_EDITOR
    public class ModeSwitcherDrawer : OdinValueDrawer<UIStateSwitcher>
    {
        private HintInfo[] hints;

        private readonly struct HintInfo
        {
            public readonly int Index;
            public readonly string IndexLabel;
            public readonly string Description;

            public HintInfo(StateSwitcherAttribute.StateDesc _info)
            {
                Index = _info.State;
                IndexLabel = _info.State.ToString();
                Description = _info.Description;
            }
        }

        protected override void Initialize()
        {
            base.Initialize();
            hints = Property.GetAttributes<StateSwitcherAttribute>()
                .SelectMany(t => t.States)
                .Select(t => new HintInfo(t))
                .ToArray();
        }

        protected override void DrawPropertyLayout(GUIContent label)
        {
            if (hints.Length <= 0)
            {
                CallNextDrawer(label);
                return;
            }

            UIStateSwitcher value = ValueEntry.SmartValue;
            bool gotValue = value;

            SirenixEditorGUI.BeginBox();

            SirenixEditorGUI.BeginBoxHeader();
            EditorGUILayout.BeginVertical();

            for (int i = 0; i < hints.Length; i++)
            {
                ref var hint = ref hints[i];
                int modeIndex = hint.Index;
                var modeInfo = value != null && value.States.Length > modeIndex && modeIndex >= 0
                    ? value.States[modeIndex]
                    : null;

                EditorGUILayout.BeginHorizontal();
                GUI.color = !gotValue || modeInfo != null ? Color.white : new Color(1f, 0.6f, 0.56f);
                var data = (modeInfo == null
                    ? $"{hint.IndexLabel}: \t{hint.Description}:\t\tNone"
                    : $"{hint.IndexLabel}: \t{hint.Description}:\t\t{'"' + modeInfo.Name + '"'}");
                EditorGUILayout.LabelField(data, SirenixGUIStyles.MultiLineLabel);
                GUI.color = Color.white;
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndVertical();
            SirenixEditorGUI.EndBoxHeader();

            CallNextDrawer(label);

            SirenixEditorGUI.EndBox();
        }
    }
#endif
*/
}