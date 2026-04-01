using Sirenix.OdinInspector;
using UnityEngine;
using Vortex.Unity.EditorTools.Attributes;

namespace Vortex.Unity.DebugSystem
{
    public partial class DebugSettings
    {
        [SerializeField] [ToggleButton(isSingleButton: true)]
        [BoxGroup("Log Settings")]
        private bool appStates;

        public bool AppStateDebugMode => DebugMode && appStates;

        [SerializeField] [ToggleButton(isSingleButton: true)]
        private bool ignorePauseInEditor;

        public bool IgnorePauseInEditor => ignorePauseInEditor;
    }
}