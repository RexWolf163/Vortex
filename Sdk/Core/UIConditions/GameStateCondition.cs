using System;
using System.Linq;
using UnityEngine;
using Vortex.Core.UIProviderSystem.Model;
using Vortex.Sdk.Core.GameCore;
using Vortex.Unity.UIProviderSystem.Model;

namespace Vortex.Sdk.Core.UIConditions
{
    /// <summary>
    /// Если игра не в одном из выбранных состояний - окно закроется
    /// </summary>
    [Serializable]
    public class GameStateCondition : UnityUserInterfaceCondition
    {
        [SerializeField] private GameStates[] states;

        protected override void Run()
        {
            GameController.OnGameStateChanged += RunCallback;
            RunCallback();
        }

        public override void DeInit()
        {
            GameController.OnGameStateChanged -= RunCallback;
        }

        public override ConditionAnswer Check()
        {
            var state = GameController.GetState();
            var result = states.Contains(state) ? ConditionAnswer.Open : ConditionAnswer.Close;
            return result;
        }
    }
}