using System;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
using Vortex.Core.Extensions.LogicExtensions;
using Vortex.Unity.AppSystem.System.TimeSystem;

namespace Vortex.Unity.LogicChainsSystem.Actions
{
    [Serializable]
    public class LoadScene : UnityLogicAction
    {
        [FormerlySerializedAs("SceneName")] [SerializeField, ValueDropdown("@DropDawnHandler.GetScenes()")]
        protected string sceneName;

        [FormerlySerializedAs("_additiveMode")] [SerializeField]
        private bool additiveMode;

        [FormerlySerializedAs("_async")] [SerializeField]
        private bool async = true;

        public override void Invoke()
        {
            TimeController.Call(() =>
            {
                if (async)
                    SceneManager.LoadSceneAsync(sceneName,
                        additiveMode ? LoadSceneMode.Additive : LoadSceneMode.Single);
                else
                    SceneManager.LoadScene(sceneName,
                        additiveMode ? LoadSceneMode.Additive : LoadSceneMode.Single);
            });
        }

        protected override string NameAction =>
            $"Call load for «{(sceneName.IsNullOrWhitespace() ? "???" : sceneName)}» scene";
    }
}