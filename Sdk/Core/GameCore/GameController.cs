using System;
using UnityEngine;
using Vortex.Core.AppSystem.Bus;
using Vortex.Core.Extensions.ReactiveValues;
using Vortex.Core.System.Abstractions;
using Vortex.Core.System.Enums;
using Vortex.Unity.AppSystem.System.TimeSystem;

namespace Vortex.Sdk.Core.GameCore
{
    /// <summary>
    /// Центральная шина-контроллер игры.
    /// Расширяется в пакетах
    /// </summary>
    public partial class GameController : Singleton<GameController>, IReactiveData
    {
        public static event Action OnNewGame;
        public static event Action OnGameStateChanged;
        public event Action OnUpdateData;

        public static event Action OnUpdate
        {
            add => Instance.OnUpdateData += value;
            remove => Instance.OnUpdateData -= value;
        }


#if UNITY_EDITOR
        public static event Action OnEditorGetData;

        private static bool editorGet;
#endif

        private static bool _newGameLock;

        #region Data

        /// <summary>
        /// Модель данных игры
        /// </summary>
        private static GameModel _data;

        public static GameStates GetState() => _data.State;

        public static T Get<T>() where T : class, GameModel.IGameData => GetData().Get<T>();

        #endregion

        #region GameControl

        /// <summary>
        /// Запуск новой игры
        /// </summary>
        public static void NewGame()
        {
            if (_newGameLock)
                return;
            _newGameLock = true;
            SetGameState(GameStates.Off);
            _data.Init();
            SetGameState(GameStates.Play);
            OnNewGame?.Invoke();
        }

        public static void ExitGame()
        {
            SetGameState(GameStates.Off);
            _newGameLock = false;
        }

        public static void Exit() => App.Exit();

        /// <summary>
        /// Поставить игру на паузу
        /// </summary>
        /// <param name="pause"></param>
        public static void SetPause(bool pause)
        {
            switch (pause)
            {
                case true when _data.State == GameStates.Play:
                    SetGameState(GameStates.Paused);
                    break;
                case false when _data.State == GameStates.Paused:
                    SetGameState(GameStates.Play);
                    break;
            }
        }

        #endregion

        #region Subscribe

        /// <summary>
        /// Подписка на обновление данных
        /// </summary>
        /// <param name="action"></param>
        [Obsolete]
        public static void Subscribe(Action action)
        {
            Instance.OnUpdateData -= action;
            Instance.OnUpdateData += action;
        }

        /// <summary>
        /// Отписка от обновления данных
        /// </summary>
        /// <param name="action"></param>
        public static void Unsubscribe(Action action)
        {
            Instance.OnUpdateData -= action;
        }

        /// <summary>
        /// Вызов централизованного события "данные обновились"
        /// </summary>
        public static void CallUpdateEvent() =>
            TimeController.Accumulate(() => Instance.OnUpdateData?.Invoke(), Instance);

        #endregion

        #region Serialization

        public static string Serialize() => _data.Serialize();

        public static void Deserialize(string json)
        {
            _data ??= new GameModel();
            _data.Deserialize(json);
            Instance.OnUpdateData?.Invoke();
        }

        #endregion

        #region Private

        /// <summary>
        /// Модель данных игры
        /// </summary>
        /// <returns></returns>
        private static GameModel GetData()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying && !editorGet)
            {
                editorGet = true;
                _data = new GameModel();
                _data.Init();
                OnEditorGetData?.Invoke();
                editorGet = false;
            }
#endif
            if (_data == null)
            {
                _data = new GameModel();
                _data.Init();
                Instance.OnUpdateData?.Invoke();
            }

            return _data;
        }

        /// <summary>
        /// Обработка глобальных состояний приложения
        /// </summary>
        /// <param name="state"></param>
        private static void OnApplicationStateChanged(AppStates state)
        {
            switch (state)
            {
                case AppStates.Unfocused:
                    SetPause(true);
                    break;
                case AppStates.Running:
                    break;
                case AppStates.Loading:
                    break;
                case AppStates.Saving:
                    break;
                case AppStates.Stopping:
                    Dispose();
                    break;
            }
        }

        private static void SetGameState(GameStates state)
        {
            if (_data == null) GetData();
            if (state == _data.State)
                return;
            _data.State = state;
            OnGameStateChanged?.Invoke();
        }

        protected override void OnInstantiate()
        {
            App.OnStateChanged += OnApplicationStateChanged;
        }

        protected override void OnDispose()
        {
            App.OnStateChanged -= OnApplicationStateChanged;
        }

        #endregion
    }
}