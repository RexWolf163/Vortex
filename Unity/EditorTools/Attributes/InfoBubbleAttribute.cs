using System;
using UnityEngine;

namespace Vortex.Unity.EditorTools.Attributes
{
    public enum InfoMessageType
    {
        None,
        Info,
        Warning,
        Error
    }

    [AttributeUsage(AttributeTargets.All, AllowMultiple = true)]
    public class InfoBubbleAttribute : PropertyAttribute
    {
        public string TextOrMethod { get; private set; }
        public InfoMessageType Icon { get; private set; }
        public string HideIf { get; private set; }

        public InfoBubbleAttribute(string textOrMethod, InfoMessageType icon = InfoMessageType.Info, string hideIf = "")
        {
            TextOrMethod = textOrMethod;
            Icon = icon;
            HideIf = hideIf;
        }
    }
}