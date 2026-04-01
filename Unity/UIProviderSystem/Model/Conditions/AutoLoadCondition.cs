using System;
using Vortex.Core.UIProviderSystem.Bus;
using Vortex.Core.UIProviderSystem.Model;

namespace Vortex.Unity.UIProviderSystem.Model.Conditions
{
    [Serializable]
    public class AutoLoadCondition : UnityUserInterfaceCondition
    {
        protected override void Run()
        {
            UIProvider.OnClose += RunCallback;
            RunCallback();
        }

        public override void DeInit()
        {
            UIProvider.OnClose -= RunCallback;
        }

        public override ConditionAnswer Check()
        {
            return ConditionAnswer.Open;
        }
    }
}