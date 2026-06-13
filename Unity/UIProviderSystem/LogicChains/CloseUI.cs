using UnityEngine;
using Vortex.Core.Extensions.LogicExtensions;
using Vortex.Core.UIProviderSystem.Bus;
using Vortex.Core.UIProviderSystem.Model;
using Vortex.Unity.AppSystem.System.TimeSystem;
using Vortex.Unity.DatabaseSystem.Attributes;
using Vortex.Unity.LogicChainsSystem.Actions;

namespace Vortex.Unity.UIProviderSystem.LogicChains
{
    /// <summary>
    /// Действие цепочки: закрыть указанный интерфейс через UIProvider.
    /// Типовое применение — снять загрузочный экран на шаге после того,
    /// как условия готовности (SystemsLoaded + NaninovelInitialized) выполнены
    /// </summary>
    public class CloseUI : UnityLogicAction
    {
        [SerializeField, DbRecord(typeof(UserInterfaceData))]
        private string uiId;

        public override void Invoke()
        {
            // Откладываем на конец кадра: Invoke зовётся из стека продвижения цепочки
            // (CheckConditions → RunChain), а закрытие UI лучше не делать реентрантно
            // внутри этого вызова — как и LoadScene
            TimeController.Accumulate(() => UIProvider.Close(uiId), this);
        }

        protected override string NameAction =>
            $"Close UI «{(uiId.IsNullOrWhitespace() ? "???" : uiId)}»";
    }
}