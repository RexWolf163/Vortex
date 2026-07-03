using System;
using Vortex.Sdk.MiniGamesSystem.MiniGames.Model;

namespace Vortex.Sdk.MiniGamesSystem.MiniGames.Abstractions
{
    /// <summary>
    /// Интерфейс для контроллера миниигры
    /// </summary>
    public interface IMiniGameHub
    {
        /// <summary>
        /// Игра закончена победой
        /// </summary>
        public event Action OnWin;

        /// <summary>
        /// Игра закончена поражением
        /// </summary>
        public event Action OnFail;

        /// <summary>
        /// Игра начата
        /// </summary>
        public event Action OnStart;

        /// <summary>
        /// Минигра закрыта
        /// </summary>
        public event Action OnExit;

        /// <summary>
        /// Глобальный конфиг миниигры
        /// </summary>
        /// <returns></returns>
        public IMiniGameConfig GetConfig();

        /// <summary>
        /// Глобальный конфиг миниигры
        /// </summary>
        /// <returns></returns>
        public IMiniGameController<MiniGameData> GetController();
    }
}