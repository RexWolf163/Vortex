using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Vortex.Unity.LogicConditionsSystem.Conditions
{
    public class SceneLoaded : UnityCondition
    {
        [SerializeField, ValueDropdown("@DropDawnHandler.GetScenes()")]
        protected string SceneName;

        private bool _completed = false;

        protected override void Start()
        {
            if (SceneManager.GetActiveScene().name == SceneName)
            {
                _completed = true;
                RunCallback();
                return;
            }

            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (SceneManager.GetActiveScene().name != SceneName)
                return;

            SceneManager.sceneLoaded -= OnSceneLoaded;
            _completed = true;
            RunCallback();
        }

        public override void DeInit() => SceneManager.sceneLoaded -= OnSceneLoaded;

        public override bool Check() => _completed;

        protected override string ConditionName => $"Wait {SceneName} loading";
    }
}