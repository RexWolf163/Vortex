using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Vortex.Core.System.ProcessInfo;

namespace Vortex.Core.SaveSystem
{
    /// <summary>
    /// Интерфейс контроллеров, чьи данные подлежат сохранению и загрузке
    /// </summary>
    public interface ISaveable
    {
        /// <summary>
        /// Ключ для данных в сохранении
        /// </summary>
        /// <returns></returns>
        public string GetSaveId();

        /// <summary>
        /// Возвращает асинхронно собираемый индекс данных
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public UniTask<Dictionary<string, string>> GetSaveData(CancellationToken cancellationToken);

        /// <summary>
        /// Указатель на данные процесса 
        /// </summary>
        /// <returns></returns>
        public ProcessData GetProcessInfo();

        /// <summary>
        /// Обработка события завершения загрузки
        /// </summary>
        public UniTask OnLoad(CancellationToken cancellationToken);
    }
}