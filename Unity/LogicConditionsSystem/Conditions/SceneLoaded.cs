using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Vortex.Unity.LogicConditionsSystem.Conditions
{
    public class SceneLoaded : UnityCondition
    {
        [SerializeField, ValueDropdown("@DropDawnHandler.GetScenes()")]
        protected string SceneName;

        protected override void Start()
        {
            if (Check())
            {
                RunCallback();
                return;
            }

            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // scene — именно загруженная сцена (в т.ч. Additive), а не активная
            if (scene.name != SceneName)
                return;

            SceneManager.sceneLoaded -= OnSceneLoaded;
            RunCallback();
        }

        public override void DeInit() => SceneManager.sceneLoaded -= OnSceneLoaded;

        // Живое состояние, а не кешированный флаг: GetSceneByName видит и Additive-сцены
        public override bool Check()
        {
            var scene = SceneManager.GetSceneByName(SceneName);
            return scene.IsValid() && scene.isLoaded;
        }

        protected override string ConditionName => $"Wait {SceneName} loading";
    }
}