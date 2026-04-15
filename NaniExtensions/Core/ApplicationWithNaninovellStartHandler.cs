using Cysharp.Threading.Tasks;
using Naninovel;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.SceneManagement;
using Vortex.Core.AppSystem.Bus;
using Vortex.Core.LoaderSystem.Bus;

namespace Vortex.NaniExtensions.Core
{
    /// <summary>
    /// Хэндлер загрузки для контроля инициализации систем Naninovell и Vortex
    /// При выполнении инициализации обеими системами, загружает настроенные сцены как Additive
    /// (Наниновел капризная на удаление стартовой сцены) 
    /// </summary>
    public class ApplicationWithNaninovellStartHandler : MonoBehaviour
    {
        [SerializeField, ValueDropdown("ScenesList")]
        private int[] scenes;

        private bool _naniWasInit;
        private bool _vortexWasInit;

        private void Start()
        {
            _naniWasInit = false;
            _vortexWasInit = false;
            Engine.OnInitializationFinished += OnNaniWasInit;
            App.OnStart += OnVortexWasInit;
            Loader.Run().Forget(Debug.LogException);
        }

        private void OnVortexWasInit()
        {
            App.OnStart -= OnVortexWasInit;
            _vortexWasInit = true;
            CheckStates();
        }

        private void OnNaniWasInit()
        {
            Engine.OnInitializationFinished -= OnNaniWasInit;
            _naniWasInit = true;
            CheckStates();
        }

        private void CheckStates()
        {
            if (!_naniWasInit || !_vortexWasInit) return;
            foreach (var sceneName in scenes)
                SceneManager.LoadScene(sceneName, LoadSceneMode.Additive);
        }

        private void OnDestroy()
        {
            Engine.OnInitializationFinished -= OnNaniWasInit;
            App.OnStart -= OnVortexWasInit;
        }

#if UNITY_EDITOR
        private ValueDropdownList<int> ScenesList()
        {
            var result = new ValueDropdownList<int>();

            var scenes = UnityEditor.EditorBuildSettings.scenes;

            for (var i = 0; i < scenes.Length; i++)
            {
                var scene = scenes[i];
                var scenePath = scene.path.Split("Scenes/")[^1].Split('.')[0];
                var scName = scenePath.Split("/")[^1].Split('.')[0];
                result.Add(scName, i);
            }

            return result;
        }
#endif
    }
}