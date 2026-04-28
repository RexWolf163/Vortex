#if UNITY_EDITOR

using UnityEngine;
using Vortex.Sdk.SdkSettingsSystem.Editor.Attribute;
using Vortex.Unity.EditorTools.Attributes;

namespace Vortex.Sdk.SdkSettingsSystem.Editor
{
    public partial class SdkSettings
    {
        [SerializeField] [DefineSymbol("USING_VORTEX_MAP_LEVELS")]
        [ToggleButton(isSingleButton: true)]
        private bool mapLevelsSdk;
    }
}
#endif