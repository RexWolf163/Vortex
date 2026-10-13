using System;
using UnityEngine;

namespace Vortex.Unity.UI.UIComponents.Attributes
{
    /// <summary>
    /// Слайдер номера части <see cref="UIComponent"/> для int-поля: от -1 до последней части указанного типа,
    /// с превью цели. Значение -1 по умолчанию — «ко всем частям»; поле со своим смыслом -1 передаёт подсказку.
    /// </summary>
    public class UIComponentLinkAttribute : PropertyAttribute
    {
        internal Type ComponentType;
        internal string Getter;

        /// <summary>Подсказка для значения -1; <c>null</c> — стандартная «ко всем компонентам».</summary>
        internal string NoneHint;

        /// <param name="componentType">Тип части компонента (<c>UIComponentText</c>, <c>UIComponentGraphic</c>…).</param>
        /// <param name="component">Имя члена с <see cref="UIComponent"/>.</param>
        /// <param name="noneHint">Подсказка для значения -1, если у поля -1 значит не «ко всем».</param>
        public UIComponentLinkAttribute(Type componentType, string component, string noneHint = null)
        {
            ComponentType = componentType;
            Getter = component;
            NoneHint = noneHint;
        }
    }
}