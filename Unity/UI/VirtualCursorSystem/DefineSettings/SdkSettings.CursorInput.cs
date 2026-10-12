using UnityEngine;
using Vortex.Sdk.SdkSettingsSystem.Attribute;
using Vortex.Unity.EditorTools.Attributes;

namespace Vortex.Sdk.SdkSettingsSystem
{
    /// <summary>
    /// Часть <see cref="SdkSettings"/> пакета виртуального курсора. Файл входит в сборку SdkSettings
    /// (через <c>.asmref</c>), поэтому тоггл виден и при выключенном модуле.
    /// </summary>
    public partial class SdkSettings
    {
        /// <summary>
        /// Включение модуля виртуального курсора: задаёт дефайн <c>USING_VORTEX_CURSOR</c>,
        /// которым гейтится сборка пакета (<c>defineConstraints</c> в asmdef).
        /// </summary>
        [SerializeField, ToggleButton(isSingleButton: true)] [DefineSymbol("USING_VORTEX_CURSOR")]
        private bool cursorInputSdk;
    }
}
