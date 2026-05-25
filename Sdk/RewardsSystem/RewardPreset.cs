using System.Collections.Generic;
using System.Linq;
using Sirenix.OdinInspector;
using UnityEngine;
using Vortex.Sdk.RewardsSystem.Model;

namespace Vortex.Sdk.RewardsSystem
{
    [OnInspectorInit("UpdatePercents")]
    [CreateAssetMenu(fileName = "RewardPreset", menuName = "Database/Reward Preset")]
    public class RewardPreset : ScriptableObject
    {
        [SerializeField] private string rewardName;

        public string Name => rewardName;

        [SerializeField, OnValueChanged("UpdatePercents")]
        private RewardPack[] rewardPacks = new RewardPack[0];

        public IReadOnlyList<RewardPack> RewardPacks => rewardPacks;

#if UNITY_EDITOR
        private void UpdatePercents()
        {
            if (rewardPacks == null || rewardPacks.Length == 0)
                return;
            var summ = rewardPacks.Sum(c => c.Weight);
            if (summ == 0)
                foreach (var aiLogic in rewardPacks)
                    aiLogic._percent = 0;
            else
                foreach (var aiLogic in rewardPacks)
                    aiLogic._percent = (float)aiLogic.Weight / summ;
        }

#endif
    }
}