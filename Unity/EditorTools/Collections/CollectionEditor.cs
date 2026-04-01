#if UNITY_EDITOR && !ODIN_INSPECTOR
using UnityEditor;
using UnityEngine;

namespace Vortex.Unity.EditorTools.Collections
{
    [CustomEditor(typeof(MonoBehaviour), true)]
    [CanEditMultipleObjects]
    public class CollectionEditor : Editor
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