using System;
using System.Linq;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Vortex.Sdk.Quests.Conditions
{
    /// <summary>
    /// Класс-коннектор для переиспользования условной логики (<see cref="QuestConditionLogic"/>) вне квестов:
    /// любая система подключает готовые квест-условия, не заводя своих типов. В квестовый цикл не входит.
    ///
    /// Группа условий для разовой проверки: TRUE, когда выполнено ЛЮБОЕ её условие (OR). Пустая
    /// группа — выполнена.
    ///
    /// В отличие от <see cref="QuestConditions"/> подписки (<see cref="QuestConditionLogic.InitListeners"/>) не
    /// ставит: условия проверяются только в момент вызова <see cref="Check"/> (наполнение витрины, выдача фразы,
    /// запуск миниигры). Поэтому OR здесь не ломает атомарное отслеживание — отслеживания нет. Для реактивных
    /// проверок — <see cref="QuestConditions"/> и <see cref="Logics.LogicGateCondition"/>.
    /// Свёртка между группами — у владельца (обычно AND: <c>groups.All(g =&gt; g.Check())</c>).
    /// </summary>
    [Serializable, HideReferenceObjectPicker]
    public class AnyConditions
    {
        [SerializeField] private string name;

        // НЕ Array.Empty<>() — общий экземпляр, Odin рисует «Reference to …». Свежий массив на экземпляр.
        [SerializeReference] private QuestConditionLogic[] conditions = new QuestConditionLogic[0];

        /// <summary>Выполнено хотя бы одно условие группы; пустая группа — выполнена</summary>
        public bool Check() => conditions.Length == 0 || conditions.Any(c => c.Check());
    }
}
