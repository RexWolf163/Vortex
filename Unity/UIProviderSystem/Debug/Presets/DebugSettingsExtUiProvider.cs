using Sirenix.OdinInspector;
using UnityEngine;
using Vortex.Unity.EditorTools.Attributes;

namespace Vortex.Unity.DebugSystem
{
    public partial class DebugSettings
    {
        [BoxGroup("Log Settings")] [SerializeField] [ToggleButton(isSingleButton: true)]
        private bool uiLogs;

        public bool UiDebugMode => DebugMode && uiLogs;
    }
}