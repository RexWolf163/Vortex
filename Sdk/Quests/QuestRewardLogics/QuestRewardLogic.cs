using System;
using Sirenix.OdinInspector;
using Vortex.Unity.EditorTools.Attributes;

namespace Vortex.Sdk.Quests.QuestRewardLogics
{
    [Serializable, HideReferenceObjectPicker, ClassLabel("$GetEditorLabel")]
    public abstract class QuestRewardLogic
    {
        /// <summary>
        /// Логики выдачи награды для прописывания в наследнике
        /// </summary>
        protected abstract void RewardLogic();

        /// <summary>
        /// Возвращает данные награды (может быть Json строка или т.п.)
        /// </summary>
        /// <returns></returns>
        public abstract string GetRewardData();

        /// <summary>
        /// Запуск логики выдачи награды.
        /// Активируется внутри пакета контроллером.
        /// </summary>
        internal void GiveReward() => RewardLogic();
    }
}