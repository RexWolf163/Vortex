using System;
using Sirenix.OdinInspector;

namespace Vortex.Sdk.Quests.Conditions
{
    /// <summary>
    /// Класс для проверки какого-то условия
    /// </summary>
    [Serializable, HideReferenceObjectPicker]
    public abstract class QuestConditionLogic
    {
        /// <summary>
        /// Проверка на отработку условия
        /// </summary>
        /// <returns></returns>
        public abstract bool Check();

        /// <summary>
        /// Подписка на проверки срабатывания.
        /// Предназначена для автоматизированного запуска проверок условий квестов на
        /// изменения реактивных данных
        ///
        /// Пример:
        ///   public override void SubscribeOnUpdate()
        ///     {
        ///        NaniWrapper.OnNaniStart += QuestController.CheckQuestStartConditions;
        ///
        ///        Альтернативная подписка для IReactiveData
        ///        QuestController.SetListener(GameController.Instance, this); 
        ///     }
        /// </summary>
        public abstract void InitListeners();

        /// <summary>
        /// Отписка от проверок на срабатывание
        /// </summary>
        public abstract void DisposeListeners();
    }
}