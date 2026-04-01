#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using Vortex.Core.AudioSystem.Bus;

namespace Vortex.Unity.AudioSystem
{
    public partial class AudioDriver
    {
        [InitializeOnLoadMethod]
        private static void EditorRegister()
        {
            if (Application.isPlaying)
                return;
            AudioController.SetDriver(Instance);
        }

        public static void ResetChannels()
        {
            AudioController.SetDriver(Instance);
        }
    }
}
#endif