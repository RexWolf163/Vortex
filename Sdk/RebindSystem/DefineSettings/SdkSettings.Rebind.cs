using UnityEngine;
using Vortex.Sdk.SdkSettingsSystem.Attribute;
using Vortex.Unity.EditorTools.Attributes;

namespace Vortex.Sdk.SdkSettingsSystem
{
    public partial class SdkSettings
    {
        /// <summary>
        /// Тумблер пакета переназначения клавиш: управляет define-символом USING_VORTEX_REBIND.
        /// Файл компилируется в сборку SdkSettings (через asmref), а не в сборку пакета — поэтому тумблер
        /// доступен и при выключенном пакете, чью сборку define-символ вырезает целиком.
        /// </summary>
        [SerializeField, ToggleButton(isSingleButton: true)] [DefineSymbol("USING_VORTEX_REBIND")]
        private bool rebindSdk;
    }
}
