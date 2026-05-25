using System;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Serialization;

namespace Vortex.Sdk.RewardsSystem.Model
{
    [Serializable]
    public class RewardData
    {
        [SerializeField] private string name;

        [SerializeReference, HideReferenceObjectPicker, FormerlySerializedAs("rewardLogic")]
        private RewardStrategy rewardStrategy;

        public string Name => name;

        internal RewardStrategy RewardStrategy => rewardStrategy;
    }
}