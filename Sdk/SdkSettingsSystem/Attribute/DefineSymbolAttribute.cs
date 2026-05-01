using System;

namespace Vortex.Sdk.SdkSettingsSystem.Attribute
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