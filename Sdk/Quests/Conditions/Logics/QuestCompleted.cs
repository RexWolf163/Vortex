using System;
using UnityEngine;
using Vortex.Unity.DatabaseSystem.Attributes;

namespace Vortex.Sdk.Quests.Conditions.Logics
{
    [Serializable]
    public class QuestCompleted : QuestConditionLogic
    {
        [SerializeField, DbRecord(typeof(QuestModel))]
        private string quest;

        public override bool Check()
        {
            var result = QuestController.IsComplete(quest);
            //Подписки не нужны, так как контроллер проверит сам  
            return result;
        }
    }
}