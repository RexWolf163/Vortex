using System;
using UnityEngine;
using Vortex.NaniExtensions.Core;

namespace Vortex.Sdk.Quests.Conditions.Logics
{
    [Serializable]
    public class NaniPlayerState : QuestConditionLogic
    {
        [SerializeField] private bool isPlay;

        public override bool Check()
        {
            var naniPlay = NaniWrapper.NaniIsPlaying();
            return isPlay == naniPlay;
        }

        public override void InitListeners()
        {
            NaniWrapper.OnNaniStart += QuestController.CheckQuestStartConditions;
            NaniWrapper.OnNaniStop += QuestController.CheckQuestStartConditions;
        }

        public override void DisposeListeners()
        {
            NaniWrapper.OnNaniStart -= QuestController.CheckQuestStartConditions;
            NaniWrapper.OnNaniStop -= QuestController.CheckQuestStartConditions;
        }
    }
}