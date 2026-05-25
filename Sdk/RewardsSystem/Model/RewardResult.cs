namespace Vortex.Sdk.RewardsSystem.Model
{
    /// <summary>
    /// Результат выдачи награды.
    /// </summary>
    public struct RewardResult
    {
        /// <summary>Награда успешно применена.</summary>
        public bool Success;

        /// <summary>Причина отказа. null при Success == true.</summary>
        public string FailReason;

        /// <summary>
        /// Фактически применённое количество (для частичной выдачи).
        /// 0 при отказе, иначе — реально выданная единица.
        /// </summary>
        public int AppliedAmount;

        public static RewardResult Ok(int amount = 1) => new()
        {
            Success = true,
            FailReason = null,
            AppliedAmount = amount
        };

        public static RewardResult Fail(string reason) => new()
        {
            Success = false,
            FailReason = reason,
            AppliedAmount = 0
        };
    }
}
