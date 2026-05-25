using System;
using System.Collections.Generic;
using System.Linq;
using Vortex.Core.Extensions.LogicExtensions;
using Vortex.Sdk.RewardsSystem.Model;

namespace Vortex.Sdk.RewardsSystem
{
    public static class RewardController
    {
        /// <summary>
        /// Выбор одной взвешенно-случайной группы наград из пресета.
        /// Возвращает DeepCopy выбранного пака — потребитель может мутировать результат
        /// (помечать выданные, изменять count и т. п.) не затрагивая исходный пресет-ассет.
        /// </summary>
        public static IReadOnlyList<RewardData> GetReward(this RewardPreset preset)
        {
            var packs = preset.RewardPacks;
            if (packs == null || packs.Count == 0)
                throw new NullReferenceException("[RewardController] Не настроены пакеты наград");
            if (packs.Count > 1)
            {
                var sum = packs.Sum(c => c.Weight);
                var random = UnityEngine.Random.Range(0, sum);
                foreach (var entry in packs)
                {
                    random -= entry.Weight;
                    if (random < 0)
                        return entry.Rewards.DeepCopy();
                }
            }

            return packs[0].Rewards.DeepCopy();
        }

        /// <summary>
        /// Проверка возможности выдачи без побочных эффектов. Для UI-превью и пред-проверок.
        /// </summary>
        public static bool ValidationRewardConditions(
            this RewardData reward,
            string targetId = null,
            float power = 1f)
            => reward.RewardStrategy.Validation(targetId, power);

        /// <summary>
        /// Синхронная выдача награды. Внутри:
        /// 1. <see cref="RewardStrategy.Validation"/> — если false, эмитит <see cref="RewardBus.OnRewardFailed"/>.
        /// 2. <see cref="RewardStrategy.GiveReward"/> — мутация модели через профильный контроллер.
        /// 3. <see cref="RewardBus.OnRewardGiven"/> либо <see cref="RewardBus.OnRewardFailed"/> по итогу.
        /// </summary>
        public static RewardResult GiveReward(
            this RewardData reward,
            string targetId = null,
            float power = 1f)
        {
            var eventData = new RewardEventData
            {
                Reward = reward,
                TargetId = targetId,
                Power = power
            };

            if (!reward.RewardStrategy.Validation(targetId, power))
            {
                eventData.Result = RewardResult.Fail("ValidationFailed");
                RewardBus.EmitFailed(eventData);
                return eventData.Result;
            }

            var result = reward.RewardStrategy.GiveReward(targetId, power);
            eventData.Result = result;

            if (result.Success)
                RewardBus.EmitGiven(eventData);
            else
                RewardBus.EmitFailed(eventData);

            return result;
        }
    }
}
