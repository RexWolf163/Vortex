using System;
using Sirenix.OdinInspector;
using UnityEngine;
using Vortex.Unity.AppSystem.System.TimeSystem;

namespace Vortex.Unity.LogicConditionsSystem.Conditions
{
    public class MinTimeCondition : UnityCondition
    {
        [SerializeField, MinValue(0f)] private float seconds;

        private DateTime target;

        protected override void Start()
        {
            target = DateTime.UtcNow.AddSeconds(seconds);
            ControlCheck();
        }

        private void ControlCheck()
        {
            if (Check())
            {
                RunCallback();
                return;
            }

            TimeController.Call(ControlCheck, (int)(target - DateTime.UtcNow).TotalSeconds, this);
        }

        public override void DeInit() => TimeController.RemoveCall(this);

        public override bool Check() => DateTime.UtcNow >= target;

        protected override string ConditionName => "Timer";
    }
}