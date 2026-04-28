#if UNITY_EDITOR

using UnityEngine;
using Vortex.Sdk.SdkSettingsSystem.Editor.Attribute;
using Vortex.Unity.EditorTools.Attributes;

namespace Vortex.Sdk.SdkSettingsSystem.Editor
{
    public partial class SdkSettings
    {
        [SerializeField, ToggleButton(isSingleButton: true)] [DefineSymbol("USING_VORTEX_SAVE_LOAD_WRAPPER")]
        private bool saveLoadWrapperSdk;
    }
}
#endif