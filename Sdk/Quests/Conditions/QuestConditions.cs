using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Vortex.Sdk.Quests.Conditions
{
    /// <summary>
    /// Группа условий, которая при запросе выдает TRUE если все ее внутренние условия выполнены по логике AND
    /// </summary>
    [Serializable, HideReferenceObjectPicker]
    public class QuestConditions
    {
        [SerializeField] private string name;
        [SerializeReference] private QuestConditionLogic[] conditions = Array.Empty<QuestConditionLogic>();

        /// <summary>
        /// Проверка на отработку всех условий группы по AND логике
        /// </summary>
        /// <returns></returns>
        public bool Check()
        {
            if (conditions.Length == 0)
                return true;

            foreach (var condition in conditions)
            {
                condition.DisposeListeners();
                if (!condition.Check())
                {
                    //Слушать надо только тех кто false. При переключении в true все равно будут запрошены все и если 
                    // они поменяли значения - это всплывет
                    // Но для страховки от утечек - предварительно Dispose для подписок
                    condition.InitListeners();
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Обнуление подписок всех Conditions для квеста
        /// </summary>
        public void DisposeListeners()
        {
            foreach (var condition in conditions)
                condition.DisposeListeners();
        }
    }
}