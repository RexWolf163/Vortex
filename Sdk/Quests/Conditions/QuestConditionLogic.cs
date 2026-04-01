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
    }
}