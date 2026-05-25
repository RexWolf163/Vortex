using System;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Serialization;

namespace Vortex.Sdk.RewardsSystem.Model
{
    /// <summary>
    /// Сериализуемая запись одной награды: пользовательское имя + полиморфная стратегия выдачи.
    /// Используется как элемент массива внутри <see cref="RewardPack"/>; в рантайме приходит
    /// в потребителя через <see cref="RewardsExtLogic.GetReward"/> уже как DeepCopy от пресета.
    ///
    /// Стратегия хранится через <c>[SerializeReference]</c> для полиморфизма в Inspector
    /// (Odin рисует дропдаун конкретного наследника <see cref="RewardStrategy"/>). Поле помечено
    /// <c>[FormerlySerializedAs("rewardLogic")]</c> — сохраняется совместимость со старыми ассетами
    /// после переименования <c>rewardLogic → rewardStrategy</c>.
    ///
    /// Доступ к стратегии умышленно <c>internal</c>: снаружи сборки её не достать, выдача и
    /// проверка идут только через extension-методы <see cref="RewardsExtLogic"/>.
    /// </summary>
    [Serializable]
    public class RewardData
    {
        [SerializeField] private string name;

        [SerializeReference, HideReferenceObjectPicker, FormerlySerializedAs("rewardLogic")]
        private RewardStrategy rewardStrategy;

        /// <summary>Пользовательское имя награды (для UI / debug-логов / редактора).</summary>
        public string Name => name;

        /// <summary>
        /// Полиморфная стратегия выдачи. Доступна только из этой сборки;
        /// внешние потребители работают через <see cref="RewardsExtLogic.GiveReward"/> и
        /// <see cref="RewardsExtLogic.ValidateRewardConditions"/>.
        /// </summary>
        internal RewardStrategy RewardStrategy => rewardStrategy;
    }
}