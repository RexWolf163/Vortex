using Sirenix.OdinInspector;
using UnityEngine;
using Vortex.Unity.EditorTools.Attributes;

namespace Vortex.Unity.DebugSystem
{
    public partial class DebugSettings
    {
        [SerializeField] [ToggleButton(isSingleButton: true)] [BoxGroup("Log Settings")]
        private bool miniGameDebugMode;

        public bool MiniGameDebugMode => DebugMode && miniGameDebugMode;
    }
}