using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.SceneManagement;
using Vortex.Core.Extensions.LogicExtensions;
using Vortex.Unity.AppSystem.System.TimeSystem;

namespace Vortex.Unity.LogicChainsSystem.Actions
{
    public class LoadScene : UnityLogicAction
    {
        [SerializeField, ValueDropdown("@DropDawnHandler.GetScenes()")]
        protected string SceneName;

        [SerializeField] private bool _additiveMode;

        [SerializeField] private bool _async = true;

        public override void Invoke()
        {
            TimeController.Call(() =>
            {
                if (_async)
                    SceneManager.LoadSceneAsync(SceneName,
                        _additiveMode ? LoadSceneMode.Additive : LoadSceneMode.Single);
                else
                    SceneManager.LoadScene(SceneName,
                        _additiveMode ? LoadSceneMode.Additive : LoadSceneMode.Single);
            });
        }

        protected override string NameAction =>
            $"Call load for «{(SceneName.IsNullOrWhitespace() ? "???" : SceneName)}» scene";
    }
}