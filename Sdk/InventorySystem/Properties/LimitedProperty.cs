#if USING_VORTEX_ITEMS
using System;
using Sirenix.OdinInspector;
using UnityEngine;
using Vortex.Sdk.ItemsSystem.Model;

namespace Vortex.Sdk.InventorySystem.Properties
{
    /// <summary>
    /// Лимит на количество этого предмета в инвентаре. Настроечное поле, сохраняемого состояния нет:
    /// <see cref="Limit"/> — геттер без сеттера, в сохранение не идёт и приходит из пресета при каждом
    /// построении. Отсутствие свойства означает безлимит; за проверку отвечает
    /// <see cref="Verifiers.LimitedVerifier"/> в наборе инвентаря.
    /// </summary>
    [Serializable]
    public class LimitedProperty : ItemProperty, ILimitedProperty
    {
        [InfoBox("Максимум единиц этого предмета в инвентаре — сумма по всем стекам")]
        [SerializeField, Min(1)]
        private int limit = 1;

        public int Limit => limit;

        protected override string Label => $"Limit: {limit}";
    }
}
#endif
