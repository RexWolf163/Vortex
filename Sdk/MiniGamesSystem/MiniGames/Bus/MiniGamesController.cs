using System;
using System.Collections.Generic;
using System.Linq;
using Vortex.Sdk.MiniGamesSystem.MiniGames.Abstractions;
using Vortex.Sdk.MiniGamesSystem.MiniGames.Controllers;
using Vortex.Sdk.MiniGamesSystem.MiniGames.Model.Statistics;
using UnityEngine;
using Vortex.Core.Extensions.LogicExtensions;
using Vortex.Sdk.Core.GameCore;
using Vortex.Sdk.MiniGamesSystem.MiniGames.Model;

namespace Vortex.Sdk.MiniGamesSystem.MiniGames.Bus
{
    /// <summary>
    /// Контроллер миниигр
    /// Фиксирует статистику, для чего выполняет подписку на события миниигр
    ///
    /// реализаторы IMiniGameController регистрируются в контроллере
    /// и снимаются с регистрации самостоятельно
    ///
    /// Этот контроллер фиксирует зарегистрированные миниигры в индексе модели
    ///
    /// Этот контроллер останавливает запущенные миниигры, если запускается новая 
    /// </summary>
    public static class MiniGamesController
    {
        public static event Action OnStartMiniGame;
        public static event Action OnWinMiniGame;
        public static event Action OnFailMiniGame;

        public static event Action OnStopMiniGame;

        /// <summary>
        /// Индекс всех миниигр проекта
        /// </summary>
        private static Dictionary<string, MiniGameObserver> _index = new();

        /// <summary>
        /// Индекс всех хабов миниигр проекта для быстрого поиска контроллера
        /// </summary>
        private static Dictionary<Type, IMiniGameController<MiniGameData>> _controllersIndex = new();

        /// <summary>
        /// Линк на модель данных
        /// </summary>
        private static MiniGamesStatisticsData _statisticsData;

        /// <summary>
        /// Линк на модель данных
        /// </summary>
        private static MiniGamesStatisticsData StatisticsData =>
            _statisticsData ??= GameController.Get<MiniGamesStatisticsData>();

        /// <summary>
        /// Кеширование последней запущенной игры
        /// </summary>
        private static string _lastStartedMiniGame;

        /// <summary>
        /// Возвращает зарегистрированный контроллер для миниигры
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static T GetController<T>() where T : class, IMiniGameController<MiniGameData>
        {
            return _controllersIndex.FirstOrDefault((p) => (typeof(T)).IsAssignableFrom(p.Key)).Value as T;
        }

        public static bool Registration(IMiniGameHub miniGameHub)
        {
            var b = _index.AddNew(miniGameHub.GetType().FullName, new MiniGameObserver(miniGameHub));
            if (!b)
                return false;

            var controller = miniGameHub.GetController();
            if (controller == null)
                return true;
            _controllersIndex.AddNew(controller.GetType(), controller);
            return true;
        }

        public static void UnRegistration(IMiniGameHub miniGameController)
        {
            var gameKey = miniGameController.GetType().FullName;
            if (gameKey.IsNullOrWhitespace() ||
                !_index.TryGetValue(gameKey, out MiniGameObserver observer))
            {
                Debug.LogWarning($"[MiniGameController] MiniGame {gameKey} is not registered.");
                return;
            }

            observer.Destroy();
            _index.Remove(gameKey);
        }

        internal static void StartGame(string miniGameKey)
        {
            //Если есть запущенная миниигра - она должна быть остановлена (засчитывается в статистике как Failed)
            foreach (var gameController in _controllersIndex.Values)
            {
                if (gameController.GetData().State == MiniGameStates.Off)
                    continue;
                if (miniGameKey.Equals(_lastStartedMiniGame))
                    continue;
                gameController.Exit();
            }

            if (!StatisticsData.Index.TryGetValue(miniGameKey, out var data))
            {
                StatisticsData.Index.Add(miniGameKey, new MiniGameStatisticData
                {
                    MiniGameKey = miniGameKey,
                    StartedGames = 1
                });
                return;
            }

            _lastStartedMiniGame = miniGameKey;
            data.StartedGames++;

            OnStartMiniGame?.Invoke();
        }

        internal static void WinGame(string miniGameKey)
        {
            if (!StatisticsData.Index.TryGetValue(miniGameKey, out var data))
            {
                Debug.LogError($"[MiniGameController] MiniGame {miniGameKey} is not registered.");
                return;
            }

            data.WinGames++;
            OnWinMiniGame?.Invoke();
        }

        internal static void FailGame(string miniGameKey)
        {
            if (!StatisticsData.Index.TryGetValue(miniGameKey, out var data))
            {
                Debug.LogError($"[MiniGameController] MiniGame {miniGameKey} is not registered.");
                return;
            }

            data.FailGames++;
            OnFailMiniGame?.Invoke();
        }

        internal static void ExitGame(string miniGameKey)
        {
            if (miniGameKey.Equals(_lastStartedMiniGame))
                _lastStartedMiniGame = null;
            if (!StatisticsData.Index.TryGetValue(miniGameKey, out var data))
            {
                Debug.LogError($"[MiniGameController] MiniGame {miniGameKey} is not registered.");
                return;
            }

            data.FailGames++;
            OnStopMiniGame?.Invoke();
        }

        /// <summary>
        /// Возвращает контроллер текущей миниигры 
        /// </summary>
        /// <returns></returns>
        public static IMiniGameController<MiniGameData> MiniGameInPlay()
        {
            foreach (var gameController in _controllersIndex.Values)
            {
                if (gameController.GetData().State == MiniGameStates.Off)
                    continue;
                return gameController;
            }

            return null;
        }

        #region Init

        [RuntimeInitializeOnLoadMethod]
        private static void Init()
        {
            _index.Clear();
            _controllersIndex.Clear();
            _statisticsData = null;
        }

        #endregion
    }
}