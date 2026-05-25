using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Vortex.Core.Extensions.LogicExtensions;
using Vortex.Sdk.RewardsSystem.Model;

namespace Vortex.Sdk.RewardsSystem
{
    /// <summary>
    /// Extension-логика поверх <see cref="RewardPreset"/> и <see cref="RewardData"/>:
    /// выбор группы наград из пресета, проверка одной награды, выдача одной награды.
    ///
    /// Батч-выдачи (<c>GiveAll</c> на списке) здесь сознательно нет. Корректность пакетной
    /// выдачи зависит от принимающей системы и не сводится к покомпонентной проверке каждой
    /// награды по отдельности. Пример: инвентарь с одним свободным слотом и две награды,
    /// каждая из которых валидна в одиночку — но вместе они уже не помещаются. Такая
    /// проверка требует знания доменной модели приёмника (инвентарь, кошелёк, прогресс) и
    /// должна жить на стороне этого приёмника, а не в обобщённой шине наград. Обход списка
    /// «как есть» через <c>foreach</c> + <see cref="GiveReward"/> допустим там, где
    /// кумулятивные эффекты заведомо отсутствуют.
    /// </summary>
    public static class RewardsExtLogic
    {
        /// <summary>
        /// Выбор одной взвешенно-случайной группы наград из пресета.
        /// Возвращает DeepCopy выбранного пака — потребитель может мутировать результат
        /// (помечать выданные, изменять count и т. п.) не затрагивая исходный пресет-ассет.
        ///
        /// Граничные случаи:
        /// <list type="bullet">
        ///   <item><description><c>packs == null</c> или пусто — <see cref="NullReferenceException"/>.</description></item>
        ///   <item><description>Ровно один пак — отдаётся всегда, его вес игнорируется.</description></item>
        ///   <item><description>Сумма весов всех паков равна 0 — возвращается <c>null</c>
        ///   (нет валидного кандидата для розыгрыша). Потребитель обязан проверять.</description></item>
        /// </list>
        /// </summary>
        public static IReadOnlyList<RewardData> GetReward(this RewardPreset preset)
        {
            var packs = preset.RewardPacks;
            if (packs == null || packs.Count == 0)
                throw new NullReferenceException("[RewardController] Не настроены пакеты наград");
            if (packs.Count > 1)
            {
                var sum = packs.Sum(c => c.Weight);
                if (sum == 0)
                    return null;
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
        ///
        /// Проверка проводится для одной награды без учета возможного кумулятивного эффекта
        /// от нескольких одновременных наград. Проверка такого кейса лежит вне этой логики
        /// </summary>
        public static bool ValidateRewardConditions(
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
            var type = reward.RewardStrategy.Type;

            var eventData = new RewardEventData
            {
                Reward = reward,
                TargetId = targetId,
                Power = power
            };

            try
            {
                if (!reward.RewardStrategy.Validation(targetId, power))
                {
                    eventData.Result = RewardResult.Fail("ValidationFailed");
                    eventData.Result.Type = type;
                    RewardBus.EmitFailed(eventData);
                    return eventData.Result;
                }

                var result = reward.RewardStrategy.GiveReward(targetId, power);
                result.Type = type;
                eventData.Result = result;

                if (result.Success)
                    RewardBus.EmitGiven(eventData);
                else
                    RewardBus.EmitFailed(eventData);

                return result;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                eventData.Result = RewardResult.Fail("Logic error");
                eventData.Result.Type = type;
                RewardBus.EmitFailed(eventData);
            }

            return eventData.Result;
        }
    }
}