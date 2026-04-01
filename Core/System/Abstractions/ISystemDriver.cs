using System;

namespace Vortex.Core.System.Abstractions
{
    public interface ISystemDriver
    {
        public event Action OnInit;

        /// <summary>
        /// Инициализация
        /// Запускается автоматически после назначения драйвера системе
        /// </summary>
        public void Init();

        /// <summary>
        /// Уничтожение
        /// Запускается автоматически при отключении драйвера от системы
        /// </summary>
        public void Destroy();
    }
}