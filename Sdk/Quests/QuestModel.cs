using System;
using Vortex.Sdk.Quests.Conditions;
using Vortex.Core.DatabaseSystem.Model;
using Vortex.Sdk.Quests.QuestRewardLogics;
using Vortex.Sdk.Quests.QuestsLogics;

namespace Vortex.Sdk.Quests
{
    [Serializable]
    public class QuestModel : Record
    {
        public event Action OnStateUpdated;
        public QuestState State { get; internal set; }

        public QuestConditions[] StartConditions { get; private set; }

        /// <summary>
        /// Условия для запуска квеста
        /// </summary>
        public QuestLogic[] Logics { get; private set; }

        /// <summary>
        /// Квест запускает автоматически если все условия запуска выполнены
        /// </summary>
        public bool Autorun { get; private set; }

        /// <summary>
        /// Квест не может закончиться как Failed. В этом случае его состояние вернется в Locked
        /// </summary>
        public bool UnFailable { get; internal set; }

        /// <summary>
        /// Сохраненный этап квеста
        /// Если при загрузке квест уже запущен, то нужно запустить его повторно с точки сохранения
        /// которая задается этим параметром 
        /// </summary>
        public byte Step { get; internal set; } = 0;

        /// <summary>
        /// Награды за выполнение квеста
        /// </summary>
        public QuestRewardLogic[] Rewards { get; internal set; }

        public override string GetDataForSave()
        {
            return $"{State};{Step}";
        }

        public override void LoadFromSaveData(string data)
        {
            var ar = data.Split(';');
            State = QuestState.Locked;
            Step = 0;
            if (ar.Length != 2)
                return;
            State = (QuestState)Enum.Parse(typeof(QuestState), ar[0]);
            Step = (byte)int.Parse(ar[1]);
        }

        internal void CallOnUpdated() => OnStateUpdated?.Invoke();
    }
}