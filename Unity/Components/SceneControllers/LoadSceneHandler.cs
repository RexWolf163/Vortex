using UnityEngine;
using UnityEngine.SceneManagement;
using Vortex.Unity.AppSystem.System.TimeSystem;

namespace Vortex.Unity.Components.SceneControllers
{
    public class LoadSceneHandler : SceneHandler
    {
        [SerializeField] private bool additiveMode;

        public override void Run()
        {
            //TimeController защищает в ситуации когда приложение запускается из редактора и происходит мерцание сцены 
            TimeController.Accumulate(
                () => SceneManager.LoadSceneAsync(sceneName,
                    additiveMode ? LoadSceneMode.Additive : LoadSceneMode.Single), this);
        }

        private void OnDestroy()
        {
            TimeController.RemoveCall(this);
        }
    }
}