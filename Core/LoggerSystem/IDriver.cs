using Vortex.Core.LoggerSystem.Model;
using Vortex.Core.System.Abstractions;

namespace Vortex.Core.LoggerSystem
{
    /// <summary>
    /// Интерфейс реализации
    /// </summary>
    public interface IDriver : ISystemDriver
    {
        /// <summary>
        /// Вывод сообщения в консоль
        /// </summary>
        /// <param name="log">структура с данными для вывода</param>
        public void Print(LogData log);
    }
}