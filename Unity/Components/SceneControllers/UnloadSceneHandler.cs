using UnityEngine.SceneManagement;

namespace Vortex.Unity.Components.SceneControllers
{
    public class UnloadSceneHandler : SceneHandler
    {
        public override void Run() => SceneManager.UnloadSceneAsync(sceneName);
    }
}