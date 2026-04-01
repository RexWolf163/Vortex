using System;
using System.Linq;
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
        public bool Check() => conditions.Length == 0 || conditions.All(c => c.Check());
    }
}