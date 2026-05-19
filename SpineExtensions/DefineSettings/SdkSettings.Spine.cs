using UnityEngine;
using Vortex.Sdk.SdkSettingsSystem.Attribute;
using Vortex.Unity.EditorTools.Attributes;

namespace Vortex.Sdk.SdkSettingsSystem
{
    public partial class SdkSettings
    {
        [SerializeField, ToggleButton(isSingleButton: true)] [DefineSymbol("USING_SPINE")]
        private bool spineExt;
    }
}