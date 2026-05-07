using System;
using UnityEngine;
using Vortex.Core.ExtensibleEnumSystem.Abstractions;

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
        /// Добавляет все значения Enum-а или ExtensibleEnum-наследника как хинты.
        /// Для Enum: описание значения можно заменить добавлением атрибута Tooltip/LabelText на значения enum-а.
        /// Для ExtensibleEnum: описание = <see cref="ExtensibleEnum.Key"/>.
        /// </summary>
        public StateSwitcherAttribute(Type type)
        {
            if (type == null)
                throw new ArgumentNullException(nameof(type));

            if (type.IsEnum)
            {
                Array values = Enum.GetValues(type);
                States = new StateDesc[values.Length];
                for (int i = 0; i < States.Length; i++)
                    States[i] = new StateDesc(values.GetValue(i));
                return;
            }

            if (typeof(ExtensibleEnum).IsAssignableFrom(type) && !type.IsAbstract)
            {
                // Реестр ExtensibleEnum заполняется eager-инициализацией в самом ExtensibleEnum
                // через [InitializeOnLoadMethod] / [RuntimeInitializeOnLoadMethod] — здесь
                // достаточно прочитать GetAll без явного RunClassConstructor.
                var values = ExtensibleEnum.GetAll(type);
                States = new StateDesc[values.Count];
                for (int i = 0; i < States.Length; i++)
                    States[i] = new StateDesc(values[i].Order, values[i].Key);
                return;
            }

            throw new ArgumentException(
                $"[StateSwitcher] {type.Name} must be an Enum or a non-abstract ExtensibleEnum subclass.");
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
}