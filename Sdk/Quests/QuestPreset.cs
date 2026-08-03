using Vortex.Sdk.Quests.Conditions;
using Sirenix.OdinInspector;
using UnityEngine;
using Vortex.Core.DatabaseSystem.Model.Enums;
using Vortex.Sdk.Quests.QuestRewardLogics;
using Vortex.Sdk.Quests.QuestsLogics;
using Vortex.Unity.DatabaseSystem.Presets;

namespace Vortex.Sdk.Quests
{
    [CreateAssetMenu(fileName = "Quest", menuName = "Database/Quest Preset")]
    public class QuestPreset : RecordPreset<QuestModel>
    {
        [SerializeReference, ListDrawerSettings(CustomAddFunction = "AddConditionsGroup")]
        [InfoBox("Условия старта: AND между группами (нужны ВСЕ — только тогда квест открывается) и внутри " +
                 "группы. Отслеживание атомарное — подписка только на первый невыполненный блокер. Не путать " +
                 "с OR-логикой условий прерывания. Пусто = стартует сразу.")]
        private QuestConditions[] startConditions;

        /// <summary>
        /// Условия для запуска квеста
        /// </summary>
        public QuestConditions[] StartConditions => startConditions;

        [InfoBox("Квест не может закончиться как Failed. В этом случае его состояние вернется в Locked")]
        [SerializeField]
        private bool unFailable;

        /// <summary>
        /// Квест не может закончиться как Failed. В этом случае его состояние вернется в Locked
        /// </summary>
        public bool UnFailable => unFailable;

        [SerializeReference, ListDrawerSettings(CustomAddFunction = "AddInterruptConditionsGroup")]
        [InfoBox("Условия прерывания: OR между группами (сработала ЛЮБАЯ — квест уходит в Blocked), " +
                 "AND внутри группы. НЕ атомарно — альтернативность, слежка за всеми группами сразу. Не путать " +
                 "с AND-логикой условий старта. Пусто = квест непрерываем.")]
        private QuestConditions[] interruptConditions = new QuestConditions[0];

        /// <summary>
        /// Условия прерывания квеста (OR между группами). Переносятся в модель тем же CopyFrom, что startConditions.
        /// </summary>
        public QuestConditions[] InterruptConditions => interruptConditions;

        [InfoBox("Разрешён обратный выход из Blocked при снятии условий прерывания. Выкл. = блок навсегда " +
                 "(до Reset/новой игры).")]
        [SerializeField]
        private bool blockRemovable;

        /// <summary>
        /// Обратимость блокировки. Переносится в модель как [NotPOCO] (из пресета, в сейв не пишется).
        /// </summary>
        public bool BlockRemovable => blockRemovable;

        [Header("Quest Logic")]
        [InfoBox("Квест запускает автоматически если все условия запуска выполнены")]
        [SerializeField]
        private bool autorun;

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

        private void AddInterruptConditionsGroup()
        {
            var ar = new QuestConditions[interruptConditions.Length + 1];
            interruptConditions.CopyTo(ar, 0);
            ar[interruptConditions.Length] = new QuestConditions();
            interruptConditions = ar;
        }

        private void OnValidate()
        {
            type = RecordTypes.MultiInstance;
        }
#endif
    }
}