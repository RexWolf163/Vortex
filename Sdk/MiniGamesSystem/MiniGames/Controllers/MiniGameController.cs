using System;
using UnityEngine;
using Vortex.Sdk.MiniGamesSystem.MiniGames.Abstractions;
using Vortex.Sdk.MiniGamesSystem.MiniGames.Model;
using Vortex.Core.Extensions.LogicExtensions;
using Vortex.Core.Extensions.LogicExtensions.SerializationSystem;
using Vortex.Core.System.Abstractions;
using Vortex.Sdk.Core.GameCore;

namespace Vortex.Sdk.MiniGamesSystem.MiniGames.Controllers
{
    public abstract class MiniGameController<T, TU> : Singleton<T>, IMiniGameController<TU>
        where T : MiniGameController<T, TU>, new()
        where TU : MiniGameData
    {
        protected TU Data;

        public virtual void Init()
        {
            Data = Activator.CreateInstance<TU>();
            Data.SetDefault();
            GameController.OnGameStateChanged += AppStateCheck;
        }

        public virtual void DeInit()
        {
            GameController.OnGameStateChanged -= AppStateCheck;
        }

        /// <summary>
        /// Возвращает ссылку на модель данных миниигры
        /// </summary>
        /// <returns></returns>
        public TU GetData() => Data;

        /// <summary>
        /// Проверка состояний приложения для их рефлексии
        /// (например для отработки паузы)
        /// </summary>
        /// <param name="state"></param>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        private void AppStateCheck()
        {
            var state = GameController.GetState();
            switch (state)
            {
                case GameStates.Paused:
                    //Если анфокус при игре - ставим паузу
                    if (Data.State == MiniGameStates.Play)
                        SetState(MiniGameStates.Paused);
                    break;
                case GameStates.Play:
                    if (Data.State == MiniGameStates.Paused)
                        SetState(MiniGameStates.Play);
                    break;
                case GameStates.Fail:
                case GameStates.Off:
                case GameStates.Win:
                case GameStates.Loading:
                    SetState(MiniGameStates.Off);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(state), state, null);
            }
        }

        public abstract void Play();

        /// <summary>
        /// Управление паузой
        /// </summary>
        /// <param name="paused"></param>
        public virtual void SetPause(bool paused = true)
        {
            switch (paused)
            {
                case true when Data.State == MiniGameStates.Play:
                    SetState(MiniGameStates.Paused);
                    break;
                case false when Data.State == MiniGameStates.Paused:
                    SetState(MiniGameStates.Play);
                    break;
            }
        }

        /// <summary>
        /// Выставить состояние модели данных
        /// </summary>
        /// <param name="state"></param>
        protected void SetState(MiniGameStates state)
        {
            Data.State = state;
            Data.CallOnStateUpdated();
            Data.CallOnUpdated();
        }

        /// <summary>
        /// Выход из миниигры
        /// </summary>
        public void Exit()
        {
            SetState(MiniGameStates.Off);
        }

        #region Cheats

        /// <summary>
        /// Авто-победа (для тестирования)
        /// </summary>
        public void CheatWin()
        {
            Debug.LogWarning($"[CHEAT] Auto Win in {GetType().Name}");
            SetState(MiniGameStates.Win);
        }

        /// <summary>
        /// Авто-поражение (для тестирования)
        /// </summary>
        public void CheatFail()
        {
            Debug.LogWarning($"[CHEAT] Auto Fail in {GetType().Name}");
            SetState(MiniGameStates.Fail);
        }

        #endregion

        #region Save/Load

        /// <summary>
        /// Возвращает строку для сохранения
        /// В минииграх избыточно, но на всякий случае пусть будет
        /// </summary>
        /// <returns></returns>
        public string GetDataForSave() => Data.SerializeProperties();

        /// <summary>
        /// Восстанавливает состояние из строки
        /// В минииграх избыточно, но на всякий случае пусть будет
        /// </summary>
        /// <param name="data"></param>
        public void LoadFromSaveData(string data) => Data.CopyFrom(data.DeserializeProperties<TU>());

        #endregion
    }
}