using UnityEngine;
using Vortex.Sdk.SdkSettingsSystem.Attribute;
using Vortex.Unity.EditorTools.Attributes;

namespace Vortex.Sdk.SdkSettingsSystem
{
    public partial class SdkSettings
    {
        /// <summary>
        /// Выбор версии озвучки реплик: выключен — AudioLocalizationSystem (V1), включён — AudioLocalizationSystemV2.
        /// Управляет define-символом USING_VORTEX_AUDIO_LOCALIZATION_V2: по нему сборки V1/V2 вырезаются
        /// defineConstraints, а канальные расширения Localization (LocalizationExt) берутся из выбранной версии.
        /// Файл компилируется в сборку SdkSettings (через asmref) — тумблер доступен при любой выбранной версии.
        /// </summary>
        [SerializeField, ToggleButton(isSingleButton: true)] [DefineSymbol("USING_VORTEX_AUDIO_LOCALIZATION_V2")]
        private bool audioLocalizationV2;
    }
}
