using Vortex.Core.UIProviderSystem.Bus;
using Vortex.Unity.AppSystem.System.TimeSystem;
using Vortex.Unity.LogicChainsSystem.Actions;

namespace Vortex.Unity.UIProviderSystem.LogicChains
{
    public class CloseAllUI : UnityLogicAction
    {
        public override void Invoke() => TimeController.Accumulate(UIProvider.CloseAll, this);
        protected override string NameAction => "Close all common UI";
    }
}