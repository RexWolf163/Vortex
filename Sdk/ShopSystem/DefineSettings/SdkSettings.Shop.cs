using UnityEngine;
using Vortex.Sdk.SdkSettingsSystem.Attribute;
using Vortex.Unity.EditorTools.Attributes;

namespace Vortex.Sdk.SdkSettingsSystem
{
    public partial class SdkSettings
    {
        /// <summary>Тумблер пакета магазина: управляет define-символом USING_VORTEX_SHOP.</summary>
        [SerializeField, ToggleButton(isSingleButton: true)] [DefineSymbol("USING_VORTEX_SHOP")]
        private bool shopSdk;
    }
}
