namespace Vortex.Sdk.RewardsSystem.Model
{
    /// <summary>
    /// Результат выдачи награды.
    ///
    /// Контракт «дискретной награды»: фактическая выдача всегда выражается в целых единицах
    /// (<see cref="AppliedAmount"/> — <c>int</c>). Параметр <c>power</c> у стратегии — это
    /// множитель силы запроса (например, «двойной дроп»), а не дробный размер награды.
    /// Стратегия применяет <c>power</c> к целочисленным шагам выдачи и при необходимости
    /// округляет/обрезает дробный остаток на своей стороне; в результат попадает уже
    /// фактически применённое целое количество.
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
        /// Всегда целое: см. контракт «дискретной награды» в описании типа.
        /// </summary>
        public int AppliedAmount;

        /// <summary>
        /// Тип награды — снимок <see cref="RewardStrategy.Type"/>, заполняется автоматически
        /// в <see cref="RewardsExtLogic.GiveReward"/>. Используется для фильтрации при
        /// сложном батчинге: группировка результатов по типу для проверки кумулятивных
        /// эффектов на стороне принимающей системы.
        ///
        /// <b>Внимание:</b> при прямом вызове <see cref="RewardStrategy.GiveReward"/> в обход
        /// extension-логики поле остаётся <c>null</c> — заполнение делает только
        /// <see cref="RewardsExtLogic.GiveReward"/>. Фабрики <see cref="Ok"/>/<see cref="Fail"/>
        /// тип не выставляют намеренно: стратегии не обязаны помнить про <c>Type</c>
        /// в каждом возврате.
        /// </summary>
        public RewardType Type;

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
