#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.SceneManagement;
using Vortex.Core.SettingsSystem.Bus;

namespace Vortex.Unity.SettingsSystem.Editor
{
    public static class StartSceneHandler
    {
        [RuntimeInitializeOnLoadMethod]
        private static void Run()
        {
            SceneManager.LoadScene(Settings.Data().StartScene);
        }
    }
}
#endif