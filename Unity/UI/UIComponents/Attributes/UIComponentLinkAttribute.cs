using System;
using UnityEngine;

namespace Vortex.Unity.UI.UIComponents.Attributes
{
    public class UIComponentLinkAttribute : PropertyAttribute
    {
        internal Type ComponentType;
        internal string Getter;

        public UIComponentLinkAttribute(Type componentType, string component)
        {
            ComponentType = componentType;
            Getter = component;
        }
    }
}