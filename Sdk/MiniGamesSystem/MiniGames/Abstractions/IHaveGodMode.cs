using System;

namespace Vortex.Sdk.MiniGamesSystem.MiniGames.Abstractions
{
    /// <summary>
    /// Интерфейс представлений с режимом "бога"
    /// В этом режиме пропускаются события которые должны привести к потере очков жизни и т.п.
    /// </summary>
    public interface IHaveGodMode
    {
        public event Action OnUpdate;

        public bool IsGodMode();

        public void SetGodMode(bool mode);
    }
}