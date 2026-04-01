using Vortex.Sdk.MiniGamesSystem.MiniGames.Abstractions;
using Vortex.Sdk.MiniGamesSystem.MiniGames.Bus;

namespace Vortex.Sdk.MiniGamesSystem.MiniGames.Controllers
{
    /// <summary>
    /// Контейнер жизненного цикла подписок, гарантирующий атомарную отписку при дерегистрации игры
    /// </summary>
    internal class MiniGameObserver
    {
        private readonly IMiniGameHub _gameHub;

        public MiniGameObserver(IMiniGameHub miniGameHub)
        {
            _gameHub = miniGameHub;
            _gameHub.OnWin += OnWin;
            _gameHub.OnFail += OnFail;
            _gameHub.OnStart += OnStart;
            _gameHub.OnExit += OnExit;
        }

        public void Destroy()
        {
            _gameHub.OnWin -= OnWin;
            _gameHub.OnFail -= OnFail;
            _gameHub.OnStart -= OnStart;
            _gameHub.OnExit -= OnExit;
        }

        private void OnStart()
        {
            MiniGamesController.StartGame(_gameHub.GetType().FullName);
        }

        private void OnFail()
        {
            MiniGamesController.FailGame(_gameHub.GetType().FullName);
        }

        private void OnWin()
        {
            MiniGamesController.WinGame(_gameHub.GetType().FullName);
        }

        private void OnExit()
        {
            MiniGamesController.ExitGame(_gameHub.GetType().FullName);
        }
    }
}