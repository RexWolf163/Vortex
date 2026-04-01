using System;
using Sirenix.OdinInspector;
using UnityEngine;
using Vortex.Core.UIProviderSystem.Model;

namespace Vortex.Unity.UIProviderSystem.Model.Conditions
{
    [Serializable]
    public class OrCondition : UnityUserInterfaceCondition
    {
        [SerializeReference, HideReferenceObjectPicker]
        private UnityUserInterfaceCondition[] conditions = new UnityUserInterfaceCondition[0];

        [SerializeField] private ConditionAnswer conditionPriority;
        [SerializeField] private ConditionAnswer notCondition;

        protected override void Run()
        {
            foreach (var condition in conditions)
                condition.Init(Data, RunCallback);

            RunCallback();
        }

        public override void DeInit()
        {
            foreach (var condition in conditions)
                condition.DeInit();
        }

        public override ConditionAnswer Check()
        {
            foreach (var condition in conditions)
                if (condition.Check() == conditionPriority)
                    return conditionPriority;

            return notCondition;
        }
    }
}