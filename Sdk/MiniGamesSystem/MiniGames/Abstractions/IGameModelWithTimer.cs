using Vortex.Unity.AppSystem.System.TimeSystem;

namespace Vortex.Sdk.MiniGamesSystem.MiniGames.Abstractions
{
    /// <summary>
    /// Интерфейс для доступа к компоненту таймера
    /// </summary>
    public interface IGameModelWithTimer
    {
        /// <summary>
        /// Доступ к таймеру
        /// </summary>
        public Timer Timer { get; }
    }
}