using System;
using UnityEngine;

namespace Vortex.Sdk.SdkSettingsSystem.Attribute
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