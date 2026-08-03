using System;
using Sirenix.OdinInspector;

namespace Vortex.Sdk.Quests.Conditions
{
    /// <summary>
    /// Класс для проверки какого-то условия.
    ///
    /// Контракт реактивности (INV-7): любое пробуждение перепроверки обязано проходить через
    /// <c>QuestController.CheckQuestStartConditions()</c> — прямым <c>+=</c> или через
    /// <c>QuestController.SetListener</c>. И старт, и прерывание перечитываются из этой единой точки;
    /// условие, будящее иной символ, выпадет из interrupt-логики (квест не заблокируется/не разблокируется).
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
        ///   public override void InitListeners()
        ///     {
        ///        ExampleEngine.OnStart += QuestController.CheckQuestStartConditions;
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