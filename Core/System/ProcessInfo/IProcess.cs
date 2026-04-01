using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Vortex.Core.System.ProcessInfo
{
    /// <summary>
    /// Интерфейс систем приложения
    /// Эти системы загружаются асинхронно и автоматически выстраивают свой порядок
    /// если их ответ на WaitingFor запрос корректен 
    /// </summary>
    public interface IProcess
    {
        /// <summary>
        /// Указатель на данные процесса 
        /// </summary>
        /// <returns></returns>
        public ProcessData GetProcessInfo();

        /// <summary>
        /// Запуск процесса
        /// </summary>
        /// <returns></returns>
        public UniTask RunAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Перечень контроллеров "пропускаемых" вперед себя
        /// </summary>
        /// <returns></returns>
        public Type[] WaitingFor();
    }
}