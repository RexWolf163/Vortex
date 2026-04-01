#if ODIN_INSPECTOR
using Sirenix.OdinInspector;
#endif
using UnityEngine;
using Vortex.Unity.EditorTools.Attributes;
using Vortex.Unity.SettingsSystem.Presets;

namespace Vortex.Unity.DebugSystem
{
    public partial class DebugSettings : SettingsPreset
    {
#if ODIN_INSPECTOR
        [PropertyOrder(-100)]
#endif
        [Position(-100)] [SerializeField] [ToggleButton(isSingleButton: true)]
        [BoxGroup("Log Settings")]
        private bool debugMode = true;

        public bool DebugMode => debugMode;
    }
}