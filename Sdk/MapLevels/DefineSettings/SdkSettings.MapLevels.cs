using UnityEngine;
using Vortex.Sdk.SdkSettingsSystem.Attribute;
using Vortex.Unity.EditorTools.Attributes;

namespace Vortex.Sdk.SdkSettingsSystem
{
    public partial class SdkSettings
    {
        [SerializeField] [DefineSymbol("USING_VORTEX_MAP_LEVELS")] [ToggleButton(isSingleButton: true)]
        private bool mapLevelsSdk = true;
    }
}