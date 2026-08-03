using System;
using Vortex.Sdk.Quests.Conditions;
using Vortex.Core.DatabaseSystem.Model;
using Vortex.Core.Extensions.LogicExtensions;
using Vortex.Core.Extensions.LogicExtensions.SerializationSystem;
using Vortex.Sdk.Quests.QuestRewardLogics;
using Vortex.Sdk.Quests.QuestsLogics;

namespace Vortex.Sdk.Quests
{
    [Serializable, POCO]
    public class QuestModel : Record
    {
        public event Action OnStateUpdated;
        public QuestState State { get; internal set; }

        public QuestConditions[] StartConditions { get; private set; }

        /// <summary>
        /// Условия прерывания квеста. Свёртка: OR между группами (сработала любая — квест блокируется),
        /// AND внутри группы. Пустой массив ⇒ квест непрерываем. POCO-сериализуемо, как StartConditions.
        /// Инициализирован пустым: старые сейвы без этого поля дают пустой набор, а не null.
        /// </summary>
        public QuestConditions[] InterruptConditions { get; private set; } = Array.Empty<QuestConditions>();

        /// <summary>
        /// Условия для запуска квеста
        /// </summary>
        public QuestLogic[] Logics { get; private set; }

        /// <summary>
        /// Квест запускает автоматически если все условия запуска выполнены
        /// </summary>
        [NotPOCO]
        public bool Autorun { get; private set; }

        /// <summary>
        /// Квест не может закончиться как Failed. В этом случае его состояние вернется в Locked
        /// </summary>
        [NotPOCO]
        public bool UnFailable { get; internal set; }

        /// <summary>
        /// Обратимость блокировки. false (по умолчанию) — Blocked навсегда до Reset/новой игры.
        /// true — квест выходит из Blocked обратно в Locked, когда условия прерывания перестали
        /// выполняться, и заново проходит турникет старта. Из пресета, в сейв не пишется — как Autorun.
        /// </summary>
        [NotPOCO]
        public bool BlockRemovable { get; private set; }

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

        public override string GetDataForSave() => this.SerializeProperties();

        public override void LoadFromSaveData(string data) => this.CopyFrom(data.DeserializeProperties<QuestModel>());

        internal void CallOnUpdated() => OnStateUpdated?.Invoke();


        /// <summary>
        /// Очистка подписок квестов
        /// Перевод в состояние Unset
        /// </summary>
        public void Reset()
        {
            State = QuestState.Unset;
            Step = 0;
            foreach (var condition in StartConditions)
                condition.DisposeListeners();
            foreach (var condition in InterruptConditions)
                condition.DisposeListeners();

            // Logics останавливаются по токену
        }
    }
}