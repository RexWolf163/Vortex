using System.Collections.Generic;
using System.Linq;
using Sirenix.OdinInspector;
using UnityEngine;
using Vortex.Sdk.RewardsSystem.Model;

namespace Vortex.Sdk.RewardsSystem
{
    /// <summary>
    /// ScriptableObject-пресет с конфигурацией одной «логической награды»: набор взвешенных
    /// групп <see cref="RewardPack"/>, из которых при выдаче случайно выбирается одна.
    ///
    /// Пресет используется по прямой ссылке (поле <c>[SerializeField] RewardPreset</c> на стороне
    /// потребителя), не через <c>Database</c> — это конфигурация дизайнера, не GUID-сущность.
    /// Меню создания: <c>Assets → Create → Database → Reward Preset</c>.
    ///
    /// Inspector: при изменении массива паков Odin вызывает <see cref="UpdatePercents"/> и
    /// перерасчёт долей вероятностей; на инициализации инспектора этот же метод вызывается
    /// через <c>[OnInspectorInit]</c>.
    /// </summary>
    [OnInspectorInit("UpdatePercents")]
    [CreateAssetMenu(fileName = "RewardPreset", menuName = "Database/Reward Preset")]
    public class RewardPreset : ScriptableObject
    {
        [SerializeField] private string rewardName;

        /// <summary>Пользовательское имя пресета (для UI / debug-логов).</summary>
        public string Name => rewardName;

        [SerializeField, OnValueChanged("UpdatePercents")]
        private RewardPack[] rewardPacks = new RewardPack[0];

        /// <summary>
        /// Группы наград, из которых одна выбирается случайно при <see cref="RewardsExtLogic.GetReward"/>.
        /// </summary>
        public IReadOnlyList<RewardPack> RewardPacks => rewardPacks;

#if UNITY_EDITOR
        /// <summary>
        /// Editor-only пересчёт <see cref="RewardPack._percent"/> для отображения долей вероятности
        /// в инспекторе. Вызывается Odin через <c>[OnValueChanged]</c> на массиве паков и
        /// <c>[OnInspectorInit]</c> на пресете. Если сумма весов 0 — все доли обнуляются.
        /// </summary>
        private void UpdatePercents()
        {
            if (rewardPacks == null || rewardPacks.Length == 0)
                return;
            var summ = rewardPacks.Sum(c => c.Weight);
            if (summ == 0)
                foreach (var pack in rewardPacks)
                    pack._percent = 0;
            else
                foreach (var pack in rewardPacks)
                    pack._percent = (float)pack.Weight / summ;
        }

#endif
    }
}