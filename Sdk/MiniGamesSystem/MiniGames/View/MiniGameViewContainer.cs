using Vortex.Sdk.MiniGamesSystem.MiniGames.Abstractions;
using Vortex.Sdk.MiniGamesSystem.MiniGames.Model;
using UnityEngine;
using Vortex.Core.System.Abstractions;
using Vortex.Unity.EditorTools.Attributes;
using Vortex.Unity.UI.Misc;

namespace Vortex.Sdk.MiniGamesSystem.MiniGames.View
{
    /// <summary>
    /// Шина представления для миниигры.
    /// Скрипт обращается к хабу и забирает конфигурацию минигры, в которой получает
    /// префаб игрового поля.
    /// Этот префаб будет размещен внутри контейнера с этим скриптом при первой активации.
    ///
    /// Важное условие!
    /// На верхнем уровне префаба должен быть DataStorage для размещения в нем ссылки на модель игры.
    /// 
    /// </summary>
    public class MiniGameViewContainer : MonoBehaviour
    {
        /// <summary>
        /// Источник конфигурации
        /// </summary>
        [SerializeField, ClassFilter(typeof(IMiniGameHub))]
        private MonoBehaviour gameHub;

        private IMiniGameHub _gameHub;

        private IMiniGameHub Hub => _gameHub ??= gameHub as IMiniGameHub;

        /// <summary>
        /// Источник модели игры
        /// </summary>
        [SerializeField, ClassFilter(typeof(IDataStorage))]
        private MonoBehaviour storage;

        private IDataStorage _storage;

        private IDataStorage Storage => _storage ??= storage as IDataStorage;

        /// <summary>
        /// Хранилище представления
        /// </summary>
        private DataStorage _viewStorage;

        private void Start()
        {
            if (Hub == null || Storage == null)
            {
                Debug.LogError($"[MiniGameViewContainer:{name}] Wrong gameHub link");
                return;
            }

            var config = Hub.GetConfig();
            var view = config.GetView();
            var go = Instantiate(view, transform);
            _viewStorage = go.GetComponent<DataStorage>();
            _viewStorage.SetData(Storage.GetData<MiniGameData>());
        }

        private void OnDestroy()
        {
            _viewStorage?.SetData(null);
        }
    }
}