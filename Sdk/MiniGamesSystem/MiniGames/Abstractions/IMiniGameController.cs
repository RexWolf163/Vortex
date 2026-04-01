using Vortex.Sdk.MiniGamesSystem.MiniGames.Model;

namespace Vortex.Sdk.MiniGamesSystem.MiniGames.Abstractions
{
    public interface IMiniGameController<out T> where T : MiniGameData
    {
        /// <summary>
        /// Запуск инициализации контроллера (подписки)
        /// </summary>
        public void Init();

        /// <summary>
        /// Освобождение ресурсов и подписок
        /// </summary>
        public void DeInit();

        /// <summary>
        /// Возвращает модель данных миниигры
        /// </summary>
        /// <returns></returns>
        public T GetData();

        /// <summary>
        /// Запуск миниигры
        /// </summary>
        public void Play();

        /// <summary>
        /// Выход из миниигры
        /// </summary>
        void Exit();

        /// <summary>
        /// Управление режимом паузы
        /// </summary>
        /// <param name="pause"></param>
        public void SetPause(bool pause);

        /// <summary>
        /// Чит-победа
        /// Для дебага или читов
        /// </summary>
        public void CheatWin();

        /// <summary>
        /// Чит-поражение
        /// Для дебага или читов
        /// </summary>
        public void CheatFail();
    }
}