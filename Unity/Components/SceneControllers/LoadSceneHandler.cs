using UnityEngine;
using UnityEngine.SceneManagement;

namespace Vortex.Unity.Components.SceneControllers
{
    public class LoadSceneHandler : SceneHandler
    {
        [SerializeField] private bool additiveMode;

        public override void Run() =>
            SceneManager.LoadSceneAsync(sceneName, additiveMode ? LoadSceneMode.Additive : LoadSceneMode.Single);
    }
}