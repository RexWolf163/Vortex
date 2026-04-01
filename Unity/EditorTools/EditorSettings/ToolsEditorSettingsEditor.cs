#if UNITY_EDITOR && !ODIN_INSPECTOR
using UnityEditor;

namespace Vortex.Unity.EditorTools.EditorSettings
{
    [CustomEditor(typeof(ToolsSettings))]
    public class ToolsSettingsEditor : Editor
    {
        private VortexRenderer renderer;

        private void OnEnable()
        {
            if (targets.Length > 0)
                renderer = new VortexRenderer(target, serializedObject, () => DrawDefaultInspector());
        }


        public override void OnInspectorGUI()
        {
            if (renderer != null)
                renderer.Render();
        }
    }
}
#endif