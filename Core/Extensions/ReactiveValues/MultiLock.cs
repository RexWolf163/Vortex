using System.Collections.Generic;
using Vortex.Core.LoggerSystem.Bus;
using Vortex.Core.LoggerSystem.Model;

namespace Vortex.Core.Extensions.ReactiveValues
{
    /// <summary>
    /// Мультизамок: заперт, пока его держит хотя бы один держатель. Каждый держатель берёт и отпускает
    /// замок сам за себя — уход одного не открывает замок, пока держат другие.
    ///
    /// Состояние — реактивное <see cref="IsLocked"/>; записывает его только сам замок
    /// </summary>
    public class MultiLock
    {
        private readonly HashSet<object> _holders = new();

        /// <summary>
        /// Замок заперт: держателей больше нуля. Владелец значения — сам замок
        /// </summary>
        public BoolData IsLocked { get; }

        /// <summary>
        /// Сколько держателей держат замок
        /// </summary>
        public int Count => _holders.Count;

        public MultiLock() => IsLocked = new BoolData(false, this);

        /// <summary>
        /// Взять замок. Повторный захват тем же держателем ничего не меняет
        /// </summary>
        /// <param name="holder">Держатель — ключ удержания</param>
        public void Lock(object holder)
        {
            if (holder == null)
            {
                Log.Print(LogLevel.Error, "MultiLock: holder is null, lock is not taken.", this);
                return;
            }

            _holders.Add(holder);
            Refresh();
        }

        /// <summary>
        /// Отпустить замок. Держатель, который замок не брал, ничего не меняет
        /// </summary>
        /// <param name="holder">Держатель — ключ удержания</param>
        public void Unlock(object holder)
        {
            if (holder == null)
            {
                Log.Print(LogLevel.Error, "MultiLock: holder is null, lock is not released.", this);
                return;
            }

            _holders.Remove(holder);
            Refresh();
        }

        /// <summary>
        /// Снять всех держателей: замок открывается
        /// </summary>
        public void Clear()
        {
            _holders.Clear();
            Refresh();
        }

        private void Refresh() => IsLocked.Set(_holders.Count > 0, this);
    }
}
