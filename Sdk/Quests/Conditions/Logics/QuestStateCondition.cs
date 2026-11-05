using System;
using System.Linq;
using UnityEngine;
using Vortex.Unity.DatabaseSystem.Attributes;

namespace Vortex.Sdk.Quests.Conditions.Logics
{
    /// <summary>
    /// Условие: состояние квеста с указанным GUID входит в белый список. Аналог <see cref="QuestCompleted"/>, но
    /// проверяет не факт завершения, а конкретные состояния (например, <c>InProgress</c> или <c>Ready</c> и
    /// <c>InProgress</c>). Пустой список — условие не выполнено. Квеста нет в текущей игре — не выполнено.
    ///
    /// Перепроверка: пока условие не выполнено, подписано на смену состояния отслеживаемого квеста
    /// (<see cref="QuestModel.OnStateUpdated"/>) — не все переходы сами запускают перепроверку условий (запуск
    /// квеста, выдача награды). Подписка — на экземпляр квеста текущей игры; при отписке ссылка обнуляется, чтобы
    /// после новой игры подписаться на новый экземпляр.
    /// </summary>
    [Serializable]
    public class QuestStateCondition : QuestConditionLogic
    {
        [SerializeField, DbRecord(typeof(QuestModel))]
        private string quest;

        [SerializeField, Tooltip("Состояния, при которых условие выполнено")]
        private QuestState[] states = new QuestState[0];

        /// <summary>Квест, на смену состояния которого подписано условие; не подписано — null</summary>
        [NonSerialized] private QuestModel _subscribed;

        public override bool Check() =>
            QuestController.TryGetQuest(quest, out var model) && states.Contains(model.State);

        public override void InitListeners()
        {
            DisposeListeners();
            if (!QuestController.TryGetQuest(quest, out var model))
                return;
            _subscribed = model;
            _subscribed.OnStateUpdated += QuestController.CheckQuestStartConditions;
        }

        public override void DisposeListeners()
        {
            if (_subscribed == null)
                return;
            _subscribed.OnStateUpdated -= QuestController.CheckQuestStartConditions;
            _subscribed = null;
        }
    }
}
