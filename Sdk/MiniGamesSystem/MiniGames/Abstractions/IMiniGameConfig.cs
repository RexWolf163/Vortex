using UnityEngine;

namespace Vortex.Sdk.MiniGamesSystem.MiniGames.Abstractions
{
    /// <summary>
    /// Интерфейс конфигурации миниигры
    /// </summary>
    public interface IMiniGameConfig
    {
        /// <summary>
        /// Представление игры
        /// </summary>
        /// <returns></returns>
        public GameObject GetView();

        /// <summary>
        /// Имя типа контролера игры
        /// </summary>
        /// <returns></returns>
        public string GetController();
    }
}