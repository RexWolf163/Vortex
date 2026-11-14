using System;
using Sirenix.OdinInspector;
using Vortex.Unity.CutsceneSystem.Bus;
using Vortex.Unity.LogicChainsSystem.Actions;

namespace Vortex.Unity.CutsceneSystem.LogicChains
{
    /// <summary>
    /// Звено «Завершение»: особый последний этап цепочки катсцены. Само показ не закрывает — отмечает, что
    /// его пора закрыть; оркестратор выгружает контент и сбрасывает состояние, когда цепочка вернёт управление.
    /// Исполняется всегда, в том числе при пролёте. Переход из его этапа завершает цепочку. Действует только
    /// на свой показ: если цепочка звена — не цепочка идущего показа, звено ничего не делает
    /// </summary>
    [Serializable]
    public class CutsceneComplete : UnityLogicAction
    {
        [ShowInInspector, DisplayAsString, HideLabel]
        private string Label => "Stop Cutscene";

        public override void Invoke() => CutsceneBus.Complete(ChainOwner);

        protected override string NameAction => "Cutscene: complete";
    }
}