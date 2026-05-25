using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Vortex.Sdk.RewardsSystem.Model
{
    [Serializable, HideReferenceObjectPicker]
    public class RewardPack
    {
        [SerializeField, HideLabel, HorizontalGroup, Range(0, 100)]
        private int weight = 50;

        public int Weight => weight;

        [SerializeField] private RewardData[] rewards;

        public IReadOnlyList<RewardData> Rewards => rewards;

#if UNITY_EDITOR
        internal float _percent;

        [ShowInInspector, HideLabel, HorizontalGroup]
        private string Percent => Mathf.Round(_percent * 100) + "%";
#endif
    }
}