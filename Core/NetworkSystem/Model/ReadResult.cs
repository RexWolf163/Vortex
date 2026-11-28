using Vortex.Core.NetworkSystem.Model.Enums;

namespace Vortex.Core.NetworkSystem.Model
{
    /// <summary>
    /// Итог чтения тела в объект класса <typeparamref name="T"/>: исход, объект (при успехе),
    /// сообщение (при неудаче).
    /// </summary>
    public sealed class ReadResult<T>
    {
        /// <summary>Исход чтения.</summary>
        public ReadStatus Status { get; }

        /// <summary>Прочитанный объект. Значим при <see cref="ReadStatus.Success"/>.</summary>
        public T Value { get; }

        /// <summary>Описание неудачи. Значимо при <see cref="ReadStatus.Failed"/>, иначе <c>null</c>.</summary>
        public string Message { get; }

        private ReadResult(ReadStatus status, T value, string message)
        {
            Status = status;
            Value = value;
            Message = message;
        }

        /// <summary>Удалось: объект получен.</summary>
        public static ReadResult<T> Success(T value) => new(ReadStatus.Success, value, null);

        /// <summary>Нет данных: тела нет.</summary>
        public static ReadResult<T> NoData() => new(ReadStatus.NoData, default, null);

        /// <summary>Не удалось: тело не разобрано.</summary>
        public static ReadResult<T> Failed(string message) => new(ReadStatus.Failed, default, message);
    }
}
