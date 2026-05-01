namespace Vortex.Sdk.Core.GameCore
{
    /// <summary>
    /// Сервис, готовности которого требуется дождаться перед переходом
    /// GameController в GameStates.Play после NewGame/OnLoad.
    /// Регистрируется через <see cref="GameController.RegisterSessionService"/>.
    /// </summary>
    public interface IGameSessionService
    {
        /// <summary>
        /// true — сервис готов, GameController может продолжить переход в Play.
        /// Опрашивается с интервалом 100ms.
        /// </summary>
        bool IsReady { get; }

        /// <summary>
        /// Имя сервиса для логов и диагностики.
        /// </summary>
        string Name => GetType().Name;
    }
}
