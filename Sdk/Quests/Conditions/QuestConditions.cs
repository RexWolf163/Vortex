using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Vortex.Sdk.Quests.Conditions
{
    /// <summary>
    /// Группа условий: TRUE, когда выполнены ВСЕ её условия (логика AND) с ленивым short-circuit.
    ///
    /// AND здесь осознан — он даёт <b>атомарное детерминированное отслеживание</b>: <see cref="Check"/>
    /// подписывается ровно на первое невыполненное условие («блокер»), а остальные проверяет только когда
    /// оно откроется (перепроверка каскадом сдвигает подписку к следующему блокеру). Блокер всегда однозначен
    /// (слева направо) — отсюда детерминированность, и активна одна подписка — отсюда атомарность.
    ///
    /// НЕ менять на OR: OR-группа стала бы истинной при открытии ЛЮБОГО из условий, значит пришлось бы
    /// подписываться на ВСЕ условия невыполненной группы (однозначного блокера нет) — это ломает атомарное
    /// отслеживание. Группировка (имя группы) — организационная, не логический OR; дерево условий вычисляется
    /// как плоское AND. Подробнее — README, раздел «Реактивная перепроверка условий».
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
        internal void DisposeListeners()
        {
            foreach (var condition in conditions)
                condition.DisposeListeners();
        }

        /// <summary>
        /// Безусловная подписка ВСЕХ условий группы — для exit-watch заблокированного квеста
        /// (BlockRemovable). <see cref="Check"/> подписывает только блокер (первое false), а у истинной
        /// группы блокера нет и подписки не будет; чтобы поймать момент, когда условие прерывания
        /// снялось, подписываемся на все условия явно. Dispose перед Init — идемпотентность, как в Check.
        /// </summary>
        internal void InitAllListeners()
        {
            foreach (var condition in conditions)
            {
                condition.DisposeListeners();
                condition.InitListeners();
            }
        }
    }
}