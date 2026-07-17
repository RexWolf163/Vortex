using System;
using Naninovel;
using Vortex.Unity.LogicConditionsSystem.Conditions;

namespace Vortex.NaniExtensions.Core.LogicChainsExt
{
    /// <summary>
    /// Условие цепочки: дождаться полной инициализации движка Naninovel.
    /// Симметрично SystemsLoaded (которое ждёт App.Running) — на одном коннекторе
    /// они дают конъюнкцию: переход только когда готовы И Vortex, И Naninovel,
    /// независимо от того, кто финишировал первым
    /// </summary>
    [Serializable]
    public class NaninovelInitialized : UnityCondition
    {
        protected override void Start()
        {
            if (Check())
            {
                RunCallback();
                return;
            }

            Engine.OnInitializationFinished += OnInitFinished;
        }

        public override void DeInit()
        {
            Engine.OnInitializationFinished -= OnInitFinished;
        }

        private void OnInitFinished()
        {
            if (!Check())
                return;
            RunCallback();
        }

        public override bool Check() => Engine.Initialized;

        protected override string ConditionName => "Wait Naninovel initialization";
    }
}