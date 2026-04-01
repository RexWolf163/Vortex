using System;
using System.Linq;
using Vortex.Core.UIProviderSystem.Model;
using UIProvider = Vortex.Core.UIProviderSystem.Bus.UIProvider;

namespace Vortex.Unity.UIProviderSystem.Model.Conditions
{
    /// <summary>
    /// Условие: Закрывать если открывается любой Common интерфейс
    /// </summary>
    [Serializable]
    public sealed class CloseOnOpenAnyUICondition : UnityUserInterfaceCondition
    {
        protected override void Run()
        {
            UIProvider.OnOpen += RunCallback;
            RunCallback();
        }

        public override void DeInit()
        {
            UIProvider.OnOpen -= RunCallback;
        }

        public override ConditionAnswer Check()
        {
            var list = UIProvider.GetOpenedUIs();
            if (list.Length == 0 || list.Contains(Data) && list.Length == 1)
                return ConditionAnswer.Idle;
            return ConditionAnswer.Close;
        }
    }
}