#if USING_VORTEX_ITEMS
using System;
using Sirenix.OdinInspector;
using UnityEngine;
using Vortex.Core.Extensions.ReactiveValues;
using Vortex.Sdk.ItemsSystem.Model;

namespace Vortex.Sdk.InventorySystem.Properties
{
    /// <summary>
    /// Свойство пачки: максимум и текущее количество. Наличие свойства плюс совпадение типа делают
    /// два предмета сливаемыми — состав прочих свойств при этом не сверяется.
    ///
    /// Количество реактивно и закрыто на владельца-контроллер. Замок вешает контроллер пачек при
    /// первом обращении, а не конструктор: экземпляр приезжает из настройки глубоким копированием,
    /// и оно перенесло бы приватного владельца из настройки, затерев назначенного.
    /// </summary>
    [Serializable]
    public class StackProperty : ItemProperty, IStackProperty
    {
        [InfoBox("Свойство пачки: максимум и текущее количество")] [SerializeField, Min(1)]
        private int max = 1;

        private bool _locked;

        /// <summary>Реактивное количество — игровое состояние, сохраняется, меняет контроллер пачек.</summary>
        public IntData CountData { get; private set; } = new(1);

        /// <summary>Максимум пачки. Настройка: getter-only, в сохранение не идёт.</summary>
        public int Max => max;

        /// <summary>Текущее количество в пачке.</summary>
        public int Count => CountData.Value;

        /// <summary>
        /// Закрыть количество на владельца-контроллер. Идемпотентно и не шумит повторным логом:
        /// признак замка — приватное поле, в сохранение не идёт и при глубоком копировании из
        /// настройки приходит сброшенным, поэтому замок ставится заново на построенном предмете.
        /// </summary>
        internal void Lock(object key)
        {
            if (_locked)
                return;
            CountData.SetOwner(key);
            _locked = true;
        }

        /// <summary>
        /// Установить количество ключом владельца, поставив замок при первом обращении. При реальном
        /// изменении и переданном владельце поднимает сигнал изменения (через защищённый метод базы):
        /// количество меняет производные величины и содержимое инвентаря. Владелец <c>null</c> —
        /// тихая установка (проба вместимости), без сигнала.
        /// </summary>
        internal void SetCount(int value, object key, ItemModel owner = null)
        {
            Lock(key);
            var before = CountData.Value;
            CountData.Set(value, key);
            if (owner != null && CountData.Value != before)
                NotifyChanged(owner);
        }

#if UNITY_EDITOR

        /// <summary>
        /// Отладочный доступ к количеству в инспекторе. Свойство, а не поле: геттер всегда отдаёт
        /// живое значение из <see cref="CountData"/> (у вложенного [Serializable]-класса нет
        /// OnValidate, синхронизировать поле было бы нечем), сеттер пишет через editor-обход замка.
        /// </summary>
        [ShowInInspector, HideInEditorMode]
        private int Count_Debug
        {
            get => CountData.Value;
            set => CountData.EditorSet(value);
        }
#endif
    }
}
#endif