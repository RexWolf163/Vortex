using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.SceneManagement;
using Vortex.Core.Extensions.LogicExtensions;
using Vortex.Unity.AppSystem.System.TimeSystem;

namespace Vortex.Unity.LogicChainsSystem.Actions
{
    public class UnLoadScene : UnityLogicAction
    {
        [SerializeField, ValueDropdown("@DropDawnHandler.GetScenes()")]
        protected string SceneName;

        [SerializeField] private bool _async = true;

        public override void Invoke()
        {
            TimeController.Call(() =>
            {
                if (_async)
                    SceneManager.UnloadSceneAsync(SceneName);
                else
                    SceneManager.UnloadScene(SceneName);
            });
        }

        protected override string NameAction =>
            $"Call unload for «{(SceneName.IsNullOrWhitespace() ? "???" : SceneName)}» scene";
    }
}