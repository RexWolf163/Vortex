using System;
using Sirenix.OdinInspector;
using Vortex.Unity.CutsceneSystem.Bus;
using Vortex.Unity.LogicChainsSystem.Actions;

namespace Vortex.Unity.CutsceneSystem.LogicChains
{
    /// <summary>
    /// Звено «Фаза»: вход в следующую фазу катсцены. Номер фазы — порядковый: каждое исполнение звена
    /// увеличивает его на единицу, контент встаёт в состояние с этим номером. При пролёте не исполняется.
    /// Не исполняется и тогда, когда цепочка звена — не цепочка идущего показа
    /// </summary>
    [Serializable]
    public class CutscenePhase : UnityLogicAction
    {
        [ShowInInspector, DisplayAsString, HideLabel]
        private string Label => "Next Cutscene Phase";

        public override void Invoke() => CutsceneBus.EnterPhase(ChainOwner);

        protected override string NameAction => "Cutscene: next phase";
    }
}