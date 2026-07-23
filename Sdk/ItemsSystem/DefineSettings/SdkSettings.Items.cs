using UnityEngine;
using Vortex.Sdk.SdkSettingsSystem.Attribute;
using Vortex.Unity.EditorTools.Attributes;

namespace Vortex.Sdk.SdkSettingsSystem
{
    public partial class SdkSettings
    {
        /// <summary>Тумблер пакета предметов: управляет define-символом USING_VORTEX_ITEMS.</summary>
        [SerializeField, ToggleButton(isSingleButton: true)] [DefineSymbol("USING_VORTEX_ITEMS")]
        private bool itemsSdk;
    }
}
