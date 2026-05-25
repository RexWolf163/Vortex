using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Vortex.Sdk.RewardsSystem.Model
{
    /// <summary>
    /// Группа наград внутри пресета: вес для взвешенного случайного выбора + набор наград,
    /// выдаваемых вместе при выборе этой группы.
    ///
    /// При вызове <see cref="RewardsExtLogic.GetReward"/> на пресете один пак выбирается случайно
    /// пропорционально <see cref="Weight"/>; возвращается DeepCopy его <see cref="Rewards"/>.
    /// Если пресет содержит ровно один пак, его вес игнорируется — он отдаётся всегда.
    /// </summary>
    [Serializable, HideReferenceObjectPicker]
    public class RewardPack
    {
        [SerializeField, HideLabel, HorizontalGroup, Range(0, 100)]
        private int weight = 50;

        /// <summary>
        /// Вес для взвешенного случайного выбора (0..100). Нулевой вес исключает пак из розыгрыша,
        /// если в пресете есть другие паки с ненулевым весом; если у всех паков вес 0 — пресет
        /// возвращает <c>null</c>.
        /// </summary>
        public int Weight => weight;

        [SerializeField] private RewardData[] rewards;

        /// <summary>
        /// Содержимое пака. Список выдаётся как есть, поштучная выдача — на стороне потребителя
        /// через <see cref="RewardsExtLogic.GiveReward"/>. Батч-валидация / батч-выдача требует
        /// знания принимающей системы и не предоставляется пакетом.
        /// </summary>
        public IReadOnlyList<RewardData> Rewards => rewards;

#if UNITY_EDITOR
        /// <summary>
        /// Отрисовываемая Editor-only доля вероятности пака (вес / сумма весов пресета).
        /// Пересчитывается в <c>RewardPreset.UpdatePercents</c> через Odin <c>[OnValueChanged]</c>.
        /// </summary>
        internal float _percent;

        [ShowInInspector, HideLabel, HorizontalGroup]
        private string Percent => Mathf.Round(_percent * 100) + "%";
#endif
    }
}