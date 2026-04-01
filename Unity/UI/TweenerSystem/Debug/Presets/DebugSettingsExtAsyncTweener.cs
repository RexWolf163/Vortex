using Sirenix.OdinInspector;
using UnityEngine;
using Vortex.Unity.EditorTools.Attributes;

namespace Vortex.Unity.DebugSystem
{
    public partial class DebugSettings
    {
        [BoxGroup("Log Settings")]
        [SerializeField] [ToggleButton(isSingleButton: true)] private bool asyncTweenerLogs;

        public bool AsyncTweenerDebugMode => DebugMode && asyncTweenerLogs;
    }
}