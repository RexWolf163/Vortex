using System;
using System.Collections.Generic;

namespace Vortex.Core.NetworkSystem
{
    /// <summary>
    /// Правила автоматического повтора одного запроса. Дефолт лежит в настройках связи; цепочка запроса получает
    /// копию дефолта и правит поля, результат запирается вместе с запросом.
    /// Разрешение повторов — параметр запроса (по умолчанию: GET — да, иные методы — только явно).
    /// </summary>
    [Serializable]
    public class RetryPolicy
    {
        /// <summary>Максимум попыток, первая входит в счёт. Не меньше 1.</summary>
        public int maxAttempts = 3;

        /// <summary>Базовая пауза перед первым повтором, секунды.</summary>
        public float baseDelay = 1f;

        /// <summary>Множитель паузы для каждого следующего повтора.</summary>
        public float multiplier = 2f;

        /// <summary>Максимальная пауза, секунды. <c>Retry-After</c> больше неё прекращает повторы.</summary>
        public float maxDelay = 30f;

        /// <summary>Разброс паузы — доля 0…1 (±).</summary>
        public float jitter = 0.2f;

        /// <summary>Учитывать заголовок ответа <c>Retry-After</c>.</summary>
        public bool respectRetryAfter = true;

        /// <summary>Повторяемые итоги: категории и HTTP-коды/диапазоны.</summary>
        public List<RetryCondition> retryOn = new()
        {
            RetryCondition.ForOutcome(QueryOutcome.Network),
            RetryCondition.ForOutcome(QueryOutcome.Timeout),
            RetryCondition.ForCodes(408, 408),
            RetryCondition.ForCodes(429, 429),
            RetryCondition.ForCodes(500, 599)
        };

        /// <summary>Глубокая копия.</summary>
        public RetryPolicy Clone()
        {
            var copy = (RetryPolicy)MemberwiseClone();
            copy.retryOn = new List<RetryCondition>(retryOn.Count);
            foreach (var condition in retryOn)
                copy.retryOn.Add(condition.Clone());
            return copy;
        }

        /// <summary>Подходит ли неудачная попытка под список повторяемых.</summary>
        internal bool IsRetryable(QueryOutcome outcome, long statusCode)
        {
            foreach (var condition in retryOn)
                if (condition.Matches(outcome, statusCode))
                    return true;
            return false;
        }

        /// <summary>Пауза перед повтором номер <paramref name="retryIndex"/> (1 — первый повтор), без учёта Retry-After.</summary>
        internal float GetDelay(int retryIndex, Random random)
        {
            var delay = baseDelay * Math.Pow(multiplier, retryIndex - 1);
            delay = Math.Min(delay, maxDelay);
            if (jitter > 0f)
                delay *= 1.0 + (random.NextDouble() * 2.0 - 1.0) * jitter;
            return (float)Math.Max(0.0, Math.Min(delay, maxDelay));
        }

        /// <summary>Проверка значений; ошибка — ошибка конфигурации.</summary>
        internal void Validate(string owner)
        {
            if (maxAttempts < 1 || baseDelay < 0f || multiplier < 1f || maxDelay < 0f || jitter < 0f || jitter > 1f)
                throw new InvalidOperationException(
                    $"[Network] Invalid retry policy in {owner}: maxAttempts ≥ 1, baseDelay ≥ 0, multiplier ≥ 1, maxDelay ≥ 0, jitter 0…1.");
            foreach (var condition in retryOn)
                if (condition == null)
                    throw new InvalidOperationException($"[Network] Empty retry condition in {owner}.");
        }
    }

    /// <summary>Повторяемый итог: категория (<see cref="QueryOutcome.Network"/>, <see cref="QueryOutcome.Timeout"/>) или диапазон HTTP-кодов.</summary>
    [Serializable]
    public class RetryCondition
    {
        public enum ConditionKind
        {
            Outcome,
            StatusCodes
        }

        public ConditionKind kind;
        public QueryOutcome outcome;
        public int codeFrom;
        public int codeTo;

        public static RetryCondition ForOutcome(QueryOutcome outcome) =>
            new() { kind = ConditionKind.Outcome, outcome = outcome };

        public static RetryCondition ForCodes(int from, int to) =>
            new() { kind = ConditionKind.StatusCodes, codeFrom = from, codeTo = to };

        internal RetryCondition Clone() => (RetryCondition)MemberwiseClone();

        internal bool Matches(QueryOutcome result, long statusCode) =>
            kind == ConditionKind.Outcome
                ? result == outcome
                : statusCode >= codeFrom && statusCode <= codeTo;
    }
}
