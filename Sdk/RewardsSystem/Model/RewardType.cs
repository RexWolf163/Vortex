using Vortex.Core.ExtensibleEnumSystem.Abstractions;

namespace Vortex.Sdk.RewardsSystem.Model
{
    /// <summary>
    /// Тип награды для фильтрации и группировки при сложном батчинге.
    /// Source of truth — <see cref="RewardStrategy.Type"/>; <see cref="RewardResult.Type"/>
    /// заполняется автоматически в <see cref="RewardsExtLogic.GiveReward"/> как снимок типа
    /// исходной стратегии.
    ///
    /// Базовый класс встроенных значений не содержит: тип награды доменный, каждое
    /// проектное/Sdk-расширение добавляет свои значения через partial-расширение
    /// (паттерн package-composition-first, аналогично <c>SettingsModel</c>).
    ///
    /// Пример расширения в пакете инвентаря:
    /// <code>
    /// public partial class RewardType
    /// {
    ///     public static readonly RewardType Item     = new(nameof(Item), 100);
    ///     public static readonly RewardType Currency = new(nameof(Currency), 110);
    /// }
    /// </code>
    /// </summary>
    public partial class RewardType : ExtensibleEnum
    {
        public RewardType(string key, int order) : base(key, order) { }
    }
}
