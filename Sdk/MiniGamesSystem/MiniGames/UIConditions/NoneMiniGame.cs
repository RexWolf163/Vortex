using System;
using Vortex.Core.UIProviderSystem.Model;
using Vortex.Sdk.MiniGamesSystem.MiniGames.Abstractions;
using Vortex.Sdk.MiniGamesSystem.MiniGames.Bus;
using Vortex.Unity.UIProviderSystem.Model;

namespace Vortex.Sdk.MiniGamesSystem.MiniGames.UIConditions
{
    [Serializable]
    public class NoneMiniGame : UnityUserInterfaceCondition
    {
        protected override void Run()
        {
            MiniGamesController.OnStartMiniGame += OnEvent;
            MiniGamesController.OnStopMiniGame += OnEvent;
            RunCallback();
        }

        public override void DeInit()
        {
            MiniGamesController.OnStartMiniGame -= OnEvent;
            MiniGamesController.OnStopMiniGame -= OnEvent;
        }

        private void OnEvent(IMiniGameHub miniGameHub) => RunCallback();

        public override ConditionAnswer Check() => MiniGamesController.MiniGameInPlay() == null
            ? ConditionAnswer.Open
            : ConditionAnswer.Close;
    }
}