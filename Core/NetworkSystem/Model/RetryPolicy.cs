using System;
using System.Collections.Generic;
using System.Linq;

namespace Vortex.Core.NetworkSystem.Model
{
    /// <summary>
    /// Правила автоповтора одного запроса: максимум попыток, базовая пауза, множитель, максимальная пауза, разброс,
    /// учёт <c>Retry-After</c>, список условий. Неизменяема: новая политика получается из прежней заменой одного
    /// значения.
    /// </summary>
    public sealed class RetryPolicy
    {
        private readonly RetryCondition[] _conditions;

        /// <summary>Максимум попыток, первая входит. Не меньше 1.</summary>
        public int MaxAttempts { get; }

        /// <summary>Базовая пауза перед повтором, секунды.</summary>
        public float BaseDelay { get; }

        /// <summary>Множитель паузы на каждую следующую попытку.</summary>
        public float DelayMultiplier { get; }

        /// <summary>Максимальная пауза, секунды.</summary>
        public float MaxDelay { get; }

        /// <summary>Разброс паузы, доля 0…1.</summary>
        public float Jitter { get; }

        /// <summary>Учитывать паузу, назначенную сервером в <c>Retry-After</c>.</summary>
        public bool RespectRetryAfter { get; }

        /// <summary>Повторяемые итоги.</summary>
        public IReadOnlyList<RetryCondition> Conditions => _conditions;

        /// <summary>Создать политику. Неверное значение — ошибка программиста или конфигурации (fast-fail).</summary>
        /// <param name="maxAttempts">Максимум попыток, первая входит. Не меньше 1.</param>
        /// <param name="baseDelay">Базовая пауза, секунды. Конечное число не меньше 0.</param>
        /// <param name="delayMultiplier">Множитель паузы. Конечное число не меньше 1.</param>
        /// <param name="maxDelay">Максимальная пауза, секунды. Конечное число не меньше 0.</param>
        /// <param name="jitter">Разброс паузы, доля 0…1.</param>
        /// <param name="respectRetryAfter">Учитывать <c>Retry-After</c>.</param>
        /// <param name="conditions">Повторяемые итоги; список копируется.</param>
        public RetryPolicy(int maxAttempts, float baseDelay, float delayMultiplier, float maxDelay, float jitter,
            bool respectRetryAfter, IEnumerable<RetryCondition> conditions)
        {
            // Сравнения записаны так, чтобы NaN не проходил ни одно из них
            if (maxAttempts < 1)
                throw Fail(new ArgumentOutOfRangeException(nameof(maxAttempts), maxAttempts,
                    "Максимум попыток политики повторов — не меньше 1 (первая попытка входит)."));
            if (!(baseDelay >= 0f) || float.IsInfinity(baseDelay))
                throw Fail(new ArgumentOutOfRangeException(nameof(baseDelay), baseDelay,
                    "Базовая пауза политики повторов — конечное число не меньше 0."));
            if (!(delayMultiplier >= 1f) || float.IsInfinity(delayMultiplier))
                throw Fail(new ArgumentOutOfRangeException(nameof(delayMultiplier), delayMultiplier,
                    "Множитель паузы политики повторов — конечное число не меньше 1."));
            if (!(maxDelay >= 0f) || float.IsInfinity(maxDelay))
                throw Fail(new ArgumentOutOfRangeException(nameof(maxDelay), maxDelay,
                    "Максимальная пауза политики повторов — конечное число не меньше 0."));
            if (!(jitter >= 0f && jitter <= 1f))
                throw Fail(new ArgumentOutOfRangeException(nameof(jitter), jitter,
                    "Разброс паузы политики повторов — доля от 0 до 1."));

            MaxAttempts = maxAttempts;
            BaseDelay = baseDelay;
            DelayMultiplier = delayMultiplier;
            MaxDelay = maxDelay;
            Jitter = jitter;
            RespectRetryAfter = respectRetryAfter;
            _conditions = conditions.ToArray();
        }

        /// <summary>Политика с другим максимумом попыток.</summary>
        public RetryPolicy WithMaxAttempts(int maxAttempts) =>
            new(maxAttempts, BaseDelay, DelayMultiplier, MaxDelay, Jitter, RespectRetryAfter, _conditions);

        /// <summary>Политика с другой базовой паузой.</summary>
        public RetryPolicy WithBaseDelay(float baseDelay) =>
            new(MaxAttempts, baseDelay, DelayMultiplier, MaxDelay, Jitter, RespectRetryAfter, _conditions);

        /// <summary>Политика с другим множителем паузы.</summary>
        public RetryPolicy WithDelayMultiplier(float delayMultiplier) =>
            new(MaxAttempts, BaseDelay, delayMultiplier, MaxDelay, Jitter, RespectRetryAfter, _conditions);

        /// <summary>Политика с другой максимальной паузой.</summary>
        public RetryPolicy WithMaxDelay(float maxDelay) =>
            new(MaxAttempts, BaseDelay, DelayMultiplier, maxDelay, Jitter, RespectRetryAfter, _conditions);

        /// <summary>Политика с другим разбросом паузы.</summary>
        public RetryPolicy WithJitter(float jitter) =>
            new(MaxAttempts, BaseDelay, DelayMultiplier, MaxDelay, jitter, RespectRetryAfter, _conditions);

        /// <summary>Политика с другим отношением к <c>Retry-After</c>.</summary>
        public RetryPolicy WithRespectRetryAfter(bool respectRetryAfter) =>
            new(MaxAttempts, BaseDelay, DelayMultiplier, MaxDelay, Jitter, respectRetryAfter, _conditions);

        /// <summary>Политика с другим списком повторяемых итогов.</summary>
        public RetryPolicy WithConditions(IEnumerable<RetryCondition> conditions) =>
            new(MaxAttempts, BaseDelay, DelayMultiplier, MaxDelay, Jitter, RespectRetryAfter, conditions);

        private static Exception Fail(Exception exception) =>
            NetworkTools.Fail(exception, nameof(RetryPolicy));
    }
}
