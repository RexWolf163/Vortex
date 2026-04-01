using Vortex.Core.UIProviderSystem.Model;
using Vortex.Sdk.MiniGamesSystem.MiniGames.Bus;
using Vortex.Unity.UIProviderSystem.Model;

namespace Vortex.Sdk.MiniGamesSystem.MiniGames.UIConditions
{
    public class NoneMiniGame : UnityUserInterfaceCondition
    {
        protected override void Run()
        {
            MiniGamesController.OnStartMiniGame += RunCallback;
            MiniGamesController.OnStopMiniGame += RunCallback;
            RunCallback();
        }

        public override void DeInit()
        {
            MiniGamesController.OnStartMiniGame -= RunCallback;
            MiniGamesController.OnStopMiniGame -= RunCallback;
        }

        public override ConditionAnswer Check() => MiniGamesController.MiniGameInPlay() == null
            ? ConditionAnswer.Open
            : ConditionAnswer.Close;
    }
}