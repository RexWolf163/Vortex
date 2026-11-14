using System;
using Vortex.Unity.CutsceneSystem.Bus;
using Vortex.Unity.LogicConditionsSystem.Conditions;

namespace Vortex.Unity.CutsceneSystem.LogicChains
{
    /// <summary>
    /// Условие «Готовность к переходу»: держит этап катсцены до сигнала «дальше» от шины — команды извне либо
    /// автоперехода контента. При пролёте считается выполненным сразу.
    ///
    /// Выполненным считается и тогда, когда цепочка условия — не цепочка идущего показа: цепочка, пережившая
    /// свой показ, не ждёт чужих сигналов, а доходит до конца вхолостую и уходит из реестра цепочек
    /// </summary>
    [Serializable]
    public class CutsceneReadyCondition : UnityCondition
    {
        private bool _ready;

        /// <summary>
        /// Условие подписано на сигнал и ждёт его
        /// </summary>
        private bool _waiting;

        /// <summary>
        /// Ждать нечего: показ пролетает либо цепочка условия осталась без показа
        /// </summary>
        private bool IsFree => CutsceneBus.IsSkipping || !CutsceneBus.IsCurrent(ChainOwner);

        protected override void Start()
        {
            _ready = false;
            if (IsFree)
            {
                RunCallback();
                return;
            }

            _waiting = true;
            CutsceneBus.OnNext += OnNext;
        }

        private void OnNext()
        {
            //Рассылка идёт по снимку подписчиков: сигнал может прийти уже после отписки
            if (!_waiting)
                return;

            _ready = true;
            RunCallback();
        }

        public override void DeInit()
        {
            CutsceneBus.OnNext -= OnNext;
            _waiting = false;
            _ready = false;
        }

        public override bool Check() => _ready || IsFree;

        protected override string ConditionName => "Cutscene: wait «Next»";
    }
}
