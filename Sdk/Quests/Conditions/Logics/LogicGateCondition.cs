using System;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;
using Vortex.Sdk.Quests.Diagnostics;
#if UNITY_EDITOR
using System.Collections.Generic;
#endif

namespace Vortex.Sdk.Quests.Conditions.Logics
{
    /// <summary>
    /// Логика свёртки набора условий в <see cref="LogicGateCondition"/>.
    /// </summary>
    public enum ConditionMode
    {
        /// <summary>Истинно, если истинно ЛЮБОЕ вложенное условие. Пустой набор ⇒ false.</summary>
        Or,

        /// <summary>Истинно, если НИ ОДНО вложенное не истинно (NOR — отрицание OR). Пустой набор ⇒ true.</summary>
        Not,

        /// <summary>Истинно, если истинно РОВНО ОДНО вложенное. Пустой набор ⇒ false.</summary>
        Xor,

        /// <summary>Истинно, если истинны ВСЕ вложенные (AND). Пустой набор ⇒ true. Добавлен В КОНЕЦ —
        /// порядок enum держит сериализованные int-значения <c>mode</c> стабильными.</summary>
        And
    }

    /// <summary>
    /// Обёртка-комбинатор над набором <see cref="QuestConditionLogic"/>: сворачивает их по выбранному
    /// <see cref="ConditionMode"/> — OR / NOT(NOR) / XOR / AND. Позволяет строить сложные деревья: сама группа
    /// (<see cref="QuestConditions"/>) сворачивает свои условия только по AND, а этот гейт даёт произвольную
    /// логику ВНУТРИ — например OR над двумя наборами, каждый из которых свёрнут по AND.
    /// Режим NOT над одним вложенным заменяет прежний флаг inverted у конкретных условий.
    ///
    /// Подписки — по принципу альтернативности (НЕ атомарности): в любом режиме результат может измениться
    /// от изменения ЛЮБОГО вложенного, поэтому <see cref="InitListeners"/> подписывает ВСЕ дочерние условия
    /// (в отличие от атомарной AND-модели группы, где слушается только первый блокер). Контракт INV-7
    /// наследуется от вложенных. Вложения произвольны — деревья AND/OR/NOT/XOR собираются свободно.
    ///
    /// Переименован из <c>OrNotCondition</c>; <see cref="MovedFromAttribute"/> сохраняет существующие
    /// сериализованные <c>[SerializeReference]</c>-ссылки (тип пишется по имени класса).
    /// </summary>
    [Serializable]
    [MovedFrom(true, sourceClassName: "OrNotCondition")]
    public class LogicGateCondition : QuestConditionLogic
    {
        [SerializeField, EnumToggleButtons, HideLabel]
        [InfoBox("OR: истинно любое.   NOT: не истинно ни одно (NOR).   XOR: истинно ровно одно.   AND: истинны все.")]
        private ConditionMode mode = ConditionMode.Or;

        // НЕ Array.Empty<>() — общий синглтон при [SerializeReference] даёт алиас между экземплярами
        // (см. QuestConditions.conditions). Свежий массив на экземпляр.
        [SerializeReference]
        private QuestConditionLogic[] conditions = new QuestConditionLogic[0];

        public override bool Check()
        {
            switch (mode)
            {
                case ConditionMode.Or:
                    for (var i = 0; i < conditions.Length; i++)
                        if (CheckAt(i))
                            return true;
                    return false;

                case ConditionMode.Not: // NOR — ни одно не истинно
                    for (var i = 0; i < conditions.Length; i++)
                        if (CheckAt(i))
                            return false;
                    return true;

                case ConditionMode.Xor: // ровно одно истинно (short-circuit при втором true)
                    var trueCount = 0;
                    for (var i = 0; i < conditions.Length; i++)
                        if (CheckAt(i) && ++trueCount > 1)
                            return false;
                    return trueCount == 1;

                case ConditionMode.And: // все истинны (short-circuit на первом false); пустой ⇒ true
                    for (var i = 0; i < conditions.Length; i++)
                        if (!CheckAt(i))
                            return false;
                    return true;

                default:
                    return false;
            }
        }

        /// <summary>
        /// Проверка вложенного условия с фиксацией результата в открытом контексте монитора квестов.
        /// </summary>
        private bool CheckAt(int index)
        {
            QuestDiagnostics.EnterNode(index);
            var result = conditions[index].Check();
            QuestDiagnostics.ExitNode(result);
            return result;
        }

#if UNITY_EDITOR
        /// <summary>Режим свёртки — для монитора квестов</summary>
        internal ConditionMode Mode => mode;

        /// <summary>Вложенные условия — для монитора квестов</summary>
        internal IReadOnlyList<QuestConditionLogic> Conditions => conditions;
#endif

        public override void InitListeners()
        {
            // В любом режиме результат зависит от всех вложенных — слушаем всех (альтернативность).
            foreach (var condition in conditions)
                condition.InitListeners();
        }

        public override void DisposeListeners()
        {
            foreach (var condition in conditions)
                condition.DisposeListeners();
        }
    }
}
