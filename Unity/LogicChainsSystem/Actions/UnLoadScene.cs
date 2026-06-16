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
    public class UnLoadScene : UnityLogicAction
    {
        [FormerlySerializedAs("SceneName")] [SerializeField, ValueDropdown("@DropDawnHandler.GetScenes()")]
        protected string sceneName;

        [FormerlySerializedAs("_async")] [SerializeField]
        private bool async = true;

        public override void Invoke()
        {
            TimeController.Call(() =>
            {
                if (async)
                    SceneManager.UnloadSceneAsync(sceneName);
                else
                    SceneManager.UnloadScene(sceneName);
            });
        }

        protected override string NameAction =>
            $"Call unload for «{(sceneName.IsNullOrWhitespace() ? "???" : sceneName)}» scene";
    }
}