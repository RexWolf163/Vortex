using Vortex.Sdk.Quests.Conditions;
using Sirenix.OdinInspector;
using UnityEngine;
using Vortex.Core.DatabaseSystem.Model.Enums;
using Vortex.Sdk.Quests.QuestRewardLogics;
using Vortex.Sdk.Quests.QuestsLogics;
using Vortex.Unity.DatabaseSystem.Presets;
using Vortex.Unity.EditorTools.Attributes;

namespace Vortex.Sdk.Quests
{
    [CreateAssetMenu(fileName = "Quest", menuName = "Database/Quest Preset")]
    public class QuestPreset : RecordPreset<QuestModel>
    {
        [SerializeReference, ListDrawerSettings(CustomAddFunction = "AddConditionsGroup")]
        private QuestConditions[] startConditions;

        /// <summary>
        /// Условия для запуска квеста
        /// </summary>
        public QuestConditions[] StartConditions => startConditions;

        [InfoBubble("Квест не может закончиться как Failed. В этом случае его состояние вернется в Locked")]
        [SerializeField]
        private bool unFailable;

        /// <summary>
        /// Квест не может закончиться как Failed. В этом случае его состояние вернется в Locked
        /// </summary>
        public bool UnFailable => unFailable;

        [SerializeField] private bool autorun;

        /// <summary>
        /// Квест запускает автоматически если все условия запуска выполнены
        /// </summary>
        public bool Autorun => autorun;

        [SerializeReference] private QuestLogic[] logic = new QuestLogic[0];

        /// <summary>
        /// Логика квеста.
        /// Состоит из очереди атомарных состояний.
        /// Квест завершается при завершении всех логик или при прерывание как Fail
        /// </summary>
        public QuestLogic[] Logics => logic;

        /// <summary>
        /// Награды за выполнение квеста
        /// </summary>
        [SerializeReference] private QuestRewardLogic[] rewards = new QuestRewardLogic[0];

        public QuestRewardLogic[] Rewards => rewards;

#if UNITY_EDITOR

        private void AddConditionsGroup()
        {
            var ar = new QuestConditions[startConditions.Length + 1];
            startConditions.CopyTo(ar, 0);
            ar[startConditions.Length] = new QuestConditions();
            startConditions = ar;
        }

        private void OnValidate()
        {
            type = RecordTypes.MultiInstance;
        }
#endif
    }
}