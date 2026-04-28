#if UNITY_EDITOR

using System;
using UnityEngine;

namespace Vortex.Sdk.SdkSettingsSystem.Editor.Attribute
{
    [AttributeUsage(AttributeTargets.Field)]
    public class DefineSymbolAttribute : PropertyAttribute
    {
        public string Define { get; private set; }

        public DefineSymbolAttribute(string define)
        {
            Define = define;
        }
    }
}
#endif