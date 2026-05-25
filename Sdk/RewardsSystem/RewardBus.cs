using System;
using Vortex.Sdk.RewardsSystem.Model;

namespace Vortex.Sdk.RewardsSystem
{
    /// <summary>
    /// Шина событий выдачи наград.
    /// Эмитит после фактической мутации модели подходящим контроллером.
    /// Подписчики реагируют на свершившийся факт (UI floating-text, achievements, quests, save-marker).
    /// </summary>
    public static class RewardBus
    {
        /// <summary>Награда успешно применена.</summary>
        public static event Action<RewardEventData> OnRewardGiven;

        /// <summary>Награду применить не удалось (валидация / отказ исполнителя).</summary>
        public static event Action<RewardEventData> OnRewardFailed;

        /// <summary>
        /// Эмит события «награда выдана». Доступен только из этой сборки — эмитить имеет
        /// право только <see cref="RewardsExtLogic.GiveReward"/> после успешной мутации модели.
        /// </summary>
        internal static void EmitGiven(RewardEventData data) => OnRewardGiven?.Invoke(data);

        /// <summary>
        /// Эмит события «награду применить не удалось». Доступен только из этой сборки;
        /// вызывается из <see cref="RewardsExtLogic.GiveReward"/> при провале валидации,
        /// неуспешном результате стратегии или перехваченном исключении.
        /// </summary>
        internal static void EmitFailed(RewardEventData data) => OnRewardFailed?.Invoke(data);
    }

    /// <summary>
    /// Снимок параметров и результата выдачи награды для подписчиков шины.
    /// Передаётся подписчикам по значению — <c>struct</c> без аллокации.
    /// </summary>
    public struct RewardEventData
    {
        /// <summary>
        /// Сама награда — рабочая копия из <see cref="RewardsExtLogic.GetReward"/>, не исходный
        /// пресет-ассет. Подписчик может читать <c>Name</c>; доступ к стратегии умышленно закрыт.
        /// </summary>
        public RewardData Reward;

        /// <summary>
        /// ID получателя награды или <c>null</c> для глобальной награды.
        /// Резолвится стратегиями через профильные шины (инвентарь, кошелёк, прогресс).
        /// </summary>
        public string TargetId;

        /// <summary>
        /// Множитель силы запроса (целочисленных шагов выдачи) — например, «двойной дроп».
        /// Не дробный размер награды: см. контракт «дискретной награды» в <see cref="RewardResult"/>.
        /// </summary>
        public float Power;

        /// <summary>
        /// Итоговый результат — <see cref="RewardResult.Success"/>, <see cref="RewardResult.FailReason"/>,
        /// <see cref="RewardResult.AppliedAmount"/>, <see cref="RewardResult.Type"/>. Тип награды
        /// доступен подписчикам через <c>Result.Type</c>, отдельного поля в EventData нет.
        /// </summary>
        public RewardResult Result;
    }
}
