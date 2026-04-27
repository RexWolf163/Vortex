#if UNITY_EDITOR

using System;

namespace Vortex.Sdk.SdkSettingsSystem.Editor.Attribute
{
    [AttributeUsage(AttributeTargets.Field)]
    public class DefineSymbolAttribute : System.Attribute
    {
        public string Define { get; private set; }

        public DefineSymbolAttribute(string define)
        {
            Define = define;
        }
    }
}
#endif