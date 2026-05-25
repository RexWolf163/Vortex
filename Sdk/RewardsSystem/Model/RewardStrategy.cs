using System;

namespace Vortex.Sdk.RewardsSystem.Model
{
    /// <summary>
    /// Стратегия выдачи награды (паттерн Strategy).
    /// Содержит данные награды и поведение её применения.
    /// Награда — это факт, не процесс. Выдача синхронна: мутация модели через профильный контроллер,
    /// представление факта (UI, звук, анимация) идёт через подписчиков <see cref="RewardBus"/>.
    /// </summary>
    [Serializable]
    public abstract class RewardStrategy
    {
        /// <summary>Человекочитаемое имя награды для UI / инспектора / debug-логов.</summary>
        public abstract string GetLabel();

        /// <summary>
        /// Тип награды для фильтрации/группировки при батчинге.
        /// Копируется как поле <see cref="RewardResult.Type"/> в результате выдачи —
        /// потребитель может разделить пакет результатов по типу (например, отдельно
        /// проверить кумулятивную совместимость всех «Item»-наград перед коммитом).
        ///
        /// Декларируется конкретной стратегией статически (не зависит от <c>targetId</c>/<c>power</c>):
        /// <c>public override RewardType Type =&gt; RewardType.Item;</c>.
        /// </summary>
        public abstract RewardType Type { get; }

        /// <summary>
        /// Можно ли выдать награду прямо сейчас.
        /// Используется UI для подсветки доступности и <see cref="RewardsExtLogic"/> перед выдачей.
        /// </summary>
        /// <param name="targetId">ID получателя (резолвится через шины). null — глобальная награда.</param>
        /// <param name="power">Множитель силы. По умолчанию 1.</param>
        public abstract bool Validation(string targetId = null, float power = 1f);

        /// <summary>
        /// Произвести выдачу награды.
        /// Метод синхронен: вносит изменения в модель данных через профильный контроллер.
        /// Событие об успехе/отказе эмитит <see cref="RewardsExtLogic"/> через <see cref="RewardBus"/>.
        /// </summary>
        /// <param name="targetId">ID получателя.</param>
        /// <param name="power">Множитель силы (для скаляр-наград).</param>
        public abstract RewardResult GiveReward(string targetId = null, float power = 1f);
    }
}
