using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Vortex.Sdk.Quests.Conditions.Logics
{
    /// <summary>
    /// Логика свёртки набора условий в <see cref="OrNotCondition"/>.
    /// </summary>
    public enum ConditionMode
    {
        /// <summary>Истинно, если истинно ЛЮБОЕ вложенное условие. Пустой набор ⇒ false.</summary>
        Or,

        /// <summary>Истинно, если НИ ОДНО вложенное не истинно (NOR — отрицание OR). Пустой набор ⇒ true.</summary>
        Not,

        /// <summary>Истинно, если истинно РОВНО ОДНО вложенное. Пустой набор ⇒ false.</summary>
        Xor
    }

    /// <summary>
    /// Обёртка-комбинатор над набором <see cref="QuestConditionLogic"/>: сворачивает их по выбранному
    /// <see cref="ConditionMode"/> — OR / NOT(NOR) / XOR. Нужна, чтобы выразить не-AND-логику ВНУТРИ группы
    /// условий — сама группа (<see cref="QuestConditions"/>) сворачивает свои условия только по AND.
    /// Режим NOT над одним вложенным условием заменяет прежний флаг inverted у конкретных условий.
    ///
    /// Подписки — по принципу альтернативности (НЕ атомарности): в любом режиме результат может измениться
    /// от изменения ЛЮБОГО вложенного, поэтому <see cref="InitListeners"/> подписывает ВСЕ дочерние условия
    /// (в отличие от атомарной AND-модели группы, где слушается только первый блокер). Контракт INV-7
    /// наследуется от вложенных. Вложения произвольны — деревья AND/OR/NOT/XOR собираются свободно.
    /// </summary>
    [Serializable]
    public class OrNotCondition : QuestConditionLogic
    {
        [SerializeField, EnumToggleButtons, HideLabel]
        [InfoBox("OR: истинно любое.   NOT: не истинно ни одно (NOR).   XOR: истинно ровно одно.")]
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
                    foreach (var condition in conditions)
                        if (condition.Check())
                            return true;
                    return false;

                case ConditionMode.Not: // NOR — ни одно не истинно
                    foreach (var condition in conditions)
                        if (condition.Check())
                            return false;
                    return true;

                case ConditionMode.Xor: // ровно одно истинно (short-circuit при втором true)
                    var trueCount = 0;
                    foreach (var condition in conditions)
                        if (condition.Check() && ++trueCount > 1)
                            return false;
                    return trueCount == 1;

                default:
                    return false;
            }
        }

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
