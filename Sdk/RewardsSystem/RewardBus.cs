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

        internal static void EmitGiven(RewardEventData data) => OnRewardGiven?.Invoke(data);
        internal static void EmitFailed(RewardEventData data) => OnRewardFailed?.Invoke(data);
    }

    /// <summary>
    /// Снимок параметров и результата выдачи награды для подписчиков шины.
    /// </summary>
    public struct RewardEventData
    {
        /// <summary>Сама награда (рабочая копия, не исходный пресет).</summary>
        public RewardData Reward;

        /// <summary>ID получателя награды или null для глобальной награды.</summary>
        public string TargetId;

        /// <summary>Множитель силы награды.</summary>
        public float Power;

        /// <summary>Итоговый результат — Success / FailReason / AppliedAmount.</summary>
        public RewardResult Result;
    }
}
