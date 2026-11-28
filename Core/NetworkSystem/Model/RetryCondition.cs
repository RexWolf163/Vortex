using System;
using Vortex.Core.NetworkSystem.Model.Enums;

namespace Vortex.Core.NetworkSystem.Model
{
    /// <summary>
    /// Один повторяемый итог: категория итога или диапазон HTTP-кодов (один код — диапазон из одного).
    /// Отвечает, подходит ли под него пара «итог, код».
    /// </summary>
    public sealed class RetryCondition
    {
        private const int MinStatus = 100;
        private const int MaxStatus = 599;

        /// <summary>Чем задано условие.</summary>
        public RetryConditionKind Kind { get; }

        /// <summary>Категория итога. Значима при <see cref="RetryConditionKind.Outcome"/>.</summary>
        public QueryOutcome Outcome { get; }

        /// <summary>Нижняя граница диапазона HTTP-кодов, включительно. Значима при <see cref="RetryConditionKind.StatusRange"/>.</summary>
        public int StatusFrom { get; }

        /// <summary>Верхняя граница диапазона HTTP-кодов, включительно. Значима при <see cref="RetryConditionKind.StatusRange"/>.</summary>
        public int StatusTo { get; }

        private RetryCondition(RetryConditionKind kind, QueryOutcome outcome, int statusFrom, int statusTo)
        {
            Kind = kind;
            Outcome = outcome;
            StatusFrom = statusFrom;
            StatusTo = statusTo;
        }

        /// <summary>Условие по категории итога: <see cref="QueryOutcome.Network"/> или <see cref="QueryOutcome.Timeout"/>.</summary>
        public static RetryCondition ForOutcome(QueryOutcome outcome)
        {
            if (outcome != QueryOutcome.Network && outcome != QueryOutcome.Timeout)
                throw Fail(new ArgumentOutOfRangeException(nameof(outcome), outcome,
                    "Категория условия повтора — только «Сеть» или «Таймаут»; HTTP-ответы задаются кодами."));
            return new RetryCondition(RetryConditionKind.Outcome, outcome, 0, 0);
        }

        /// <summary>Условие по одному HTTP-коду.</summary>
        public static RetryCondition ForStatus(int statusCode) => ForStatusRange(statusCode, statusCode);

        /// <summary>Условие по диапазону HTTP-кодов, границы включительно, в пределах 100…599.</summary>
        public static RetryCondition ForStatusRange(int statusFrom, int statusTo)
        {
            if (statusFrom < MinStatus || statusTo > MaxStatus || statusFrom > statusTo)
                throw Fail(new ArgumentException(
                    $"Диапазон HTTP-кодов условия повтора неверен: {statusFrom}–{statusTo}. " +
                    $"Допустимо {MinStatus}–{MaxStatus}, нижняя граница не больше верхней."));
            return new RetryCondition(RetryConditionKind.StatusRange, QueryOutcome.None, statusFrom, statusTo);
        }

        /// <summary>Подходит ли под условие пара «итог попытки, HTTP-код».</summary>
        public bool Matches(QueryOutcome outcome, int statusCode) =>
            Kind == RetryConditionKind.Outcome
                ? outcome == Outcome
                : statusCode >= StatusFrom && statusCode <= StatusTo;

        private static Exception Fail(Exception exception) =>
            NetworkTools.Fail(exception, nameof(RetryCondition));
    }
}
