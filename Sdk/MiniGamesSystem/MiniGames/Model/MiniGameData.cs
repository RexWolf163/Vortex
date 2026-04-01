using System;

namespace Vortex.Sdk.MiniGamesSystem.MiniGames.Model
{
    public abstract class MiniGameData
    {
        /// <summary>
        /// Изменение конкретно состояния игры
        /// </summary>
        public event Action<MiniGameStates> OnGameStateChanged;

        /// <summary>
        /// Общее событие изменения состояния модели данных
        /// </summary>
        public event Action OnUpdated;

        /// <summary>
        /// Текущее состояние миниигры
        /// </summary>
        public MiniGameStates State { get; protected internal set; }

        /// <summary>
        /// Возвращение модели к дефолтным настройкам
        /// </summary>
        protected internal abstract void SetDefault();

        /// <summary>
        /// Вызов события "Состояние обновилось"
        /// </summary>
        protected internal void CallOnStateUpdated() => OnGameStateChanged?.Invoke(State);

        /// <summary>
        /// Вызов события "данные обновились"
        /// </summary>
        public void CallOnUpdated() => OnUpdated?.Invoke();
    }
}