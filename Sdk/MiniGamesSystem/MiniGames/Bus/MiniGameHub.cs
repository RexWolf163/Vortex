using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Vortex.Core.System.Abstractions;
using Vortex.Sdk.MiniGamesSystem.MiniGames.Abstractions;
using Vortex.Sdk.MiniGamesSystem.MiniGames.Model;
using Vortex.Unity.AppSystem.System.TimeSystem;
using Vortex.Unity.EditorTools.Attributes;
using Vortex.Unity.Extensions.Abstractions;

namespace Vortex.Sdk.MiniGamesSystem.MiniGames.Bus
{
    /// <summary>
    /// Хаб доступа к миниигре
    /// Содержит логику состояний миниигры, и принимает параметры для запуска
    /// Логика ограничений для запуска или иная логика не относящаяся к игровому процессу может быть здесь 
    ///
    /// Текущая реализация подразумевает строго один экземпляр миниигры на приложение
    /// Хаб запускает игру по запросу, после чего отслеживает изменение ее состояния по модели данных
    /// Хаб настраивается на один вариант контроллера через ассет конфигурации (direct DI)
    /// 
    /// </summary>
    public abstract class MiniGameHub<T, TD, TCf, TC> : MonoBehaviourSingleton<T>, IMiniGameHub, IDataStorage
        where T : MiniGameHub<T, TD, TCf, TC>
        where TD : MiniGameData
        where TC : class, IMiniGameController<TD>
        where TCf : class, IMiniGameConfig
    {
        #region Events

        /// <summary>
        /// Событие победы в игре
        /// </summary>
        public event Action OnWin;

        /// <summary>
        /// Событие поражения в игре
        /// </summary>
        public event Action OnFail;

        /// <summary>
        /// Событие завершения игры
        /// </summary>
        public event Action OnExit;

        /// <summary>
        /// Событие начала игры
        /// </summary>
        public event Action OnStart;

        protected void CallOnWin() => OnWin?.Invoke();

        protected void CallOnFail() => OnFail?.Invoke();

        protected void CallOnStart() => OnStart?.Invoke();

        #endregion

        /// <summary>
        /// Модель данных миниигры
        /// </summary>
        public static TD Data => Instance?.Controller.GetData();

        [InfoBubble("Ассет с базовой конфигурацией минигры")] [SerializeField]
        protected TCf config;

        /// <summary>
        /// Контроллер миниигры с ленивой активацией
        /// </summary>
        protected TC Controller
        {
            get
            {
                if (_controller != null) return _controller;

                var typeName = config.GetController();
                var type = Type.GetType(typeName);
                if (type == null)
                    throw new NullReferenceException($"The type «{typeName}» could not be found.");
                _controller = Activator.CreateInstance(type) as TC;
                _controller?.Init();
                OnUpdateLink?.Invoke();

                return _controller;
            }
        }

        private TC _controller;

        private bool _isRegistered = false;

        /// <summary>
        /// Возвращает установленный контроллер миниигры
        /// </summary>
        /// <returns></returns>
        public IMiniGameController<MiniGameData> GetController() => Instance.Controller;

        /// <summary>
        /// Запуск игры асинхронно, для отслеживания завершения процесса снаружи
        /// </summary>
        /// <param name="playConfig">Стартовая конфигурация игры</param>
        public static async UniTask Play(object playConfig = null)
        {
            await Instance.PlayMiniGame(playConfig);
        }

        /// <summary>
        /// Управление паузой
        /// </summary>
        /// <param name="pause"></param>
        public void SetPause(bool pause) => Controller?.SetPause(pause);

        /// <summary>
        /// Запрос выхода из миниигры
        /// </summary>
        public void Exit()
        {
            Controller?.Exit();
            OnExit?.Invoke();
        }

        /// <summary>
        /// Глобальный конфиг миниигры
        /// </summary>
        public IMiniGameConfig GetConfig() => config;

        /// <summary>
        /// Возвращает состояние миниигры
        /// </summary>
        /// <returns></returns>
        public static MiniGameStates GetState() => Data.State;

        /// <summary>
        /// Запуск игры асинхронно, для отслеживания завершения процесса снаружи
        /// </summary>
        /// <param name="playConfig">Стартовая конфигурация игры</param>
        protected virtual async UniTask PlayMiniGame(object playConfig)
        {
            Debug.LogError($"[{GetType().Name}] Broken game hub Play logic!");
            await UniTask.Yield();
        }

        #region Init/Destroy

        protected override void Awake()
        {
            //Валидация данных
            var error = config == null;

            if (error)
            {
                Debug.LogError($"[{GetType().Name}] Configuration incomplete for {gameObject.name} ");
                return;
            }

            TimeController.Accumulate(() => { _isRegistered = MiniGamesController.Registration(this); }, this);
            base.Awake();
        }

        protected override void OnDestroy()
        {
            TimeController.RemoveCall(this);
            if (_isRegistered)
                MiniGamesController.UnRegistration(this);
            _isRegistered = false;
            base.OnDestroy();
        }

        #endregion

        #region IDataStorage

        //Служебный регион реализации логики IDataStorage

        /// <summary>
        /// Событие обновления данных в контейнере
        /// </summary>
        public event Action OnUpdateLink;

        /// <summary>
        /// Возвращает данные миниигры
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public T GetData<T>() where T : class
        {
            var type = typeof(T);
            T data;
            if (type == typeof(IMiniGameController<MiniGameData>))
                data = Controller as T;
            else
                data = Data as T;

            if (data == null)
                Debug.LogError($"[{GetType().Name}] Wrong data type «{typeof(T).Name}» requested.");
            return data;
        }

        #endregion
    }
}