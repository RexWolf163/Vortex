using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Vortex.Core.LoggerSystem.Bus;
using Vortex.Core.LoggerSystem.Model;
using Vortex.Core.SaveSystem.Abstraction;
using Vortex.Core.SaveSystem.Model;
using Vortex.Core.System.Abstractions;
using Vortex.Core.System.ProcessInfo;

namespace Vortex.Core.SaveSystem.Bus
{
    /// <summary>
    /// Шина слотов сохранения. Save/Load/Remove не выполняются сразу, а встают в очередь операций и исполняются
    /// по одной, каждая — после завершения предыдущей (см. <c>SaveController.Queue.cs</c>).
    /// </summary>
    public partial class SaveController : SystemController<SaveController, IDriver>
    {
        private static readonly Dictionary<string, Dictionary<string, string>> SaveDataIndex = new();

        private static readonly HashSet<ISaveable> Saveables = new();

        /// <summary>
        /// Токен-ресурс прерывания
        /// Добавлен на будущее. На данный момент не используется
        /// </summary>
        private static CancellationTokenSource _cancelTokenSource = new();

        /// <summary>
        /// Токен прерывания
        /// </summary>
        private static CancellationToken Token => _cancelTokenSource.Token;

        /// <summary>
        /// Событие начала загрузки
        /// </summary>
        public static event Action OnLoadStart;

        /// <summary>
        /// Запрос сохранения принят. Вызывается синхронно внутри <see cref="Save"/>, до постановки в очередь, — здесь
        /// независимые контроллеры записывают метаданные момента запроса (превью, режим, тип сейва) через
        /// <see cref="AddMeta"/>: они едут вместе с запросом и попадут в сводку, даже если сохранение исполнится позже.
        /// Отклонённый запрос события не поднимает. Вызывать <see cref="Save"/> из обработчика нельзя.
        /// </summary>
        public static event Action OnSaveRequest;

        /// <summary>
        /// Событие начала исполнения сохранения. Сводка цикла уже заведена (<see cref="IDriver.BeginSummary"/>) и
        /// содержит метаданные запроса; <see cref="AddMeta"/> здесь пишет метаданные момента исполнения.
        /// </summary>
        public static event Action OnSaveStart;

        /// <summary>
        /// Событие завершения загрузки
        /// </summary>
        public static event Action OnLoadComplete;

        /// <summary>
        /// Событие завершения сохранения
        /// </summary>
        public static event Action OnSaveComplete;

        /// <summary>
        /// Событие удаления сохранения
        /// </summary>
        public static event Action OnRemove;

        /// <summary>
        /// Данные общего процесса
        /// </summary>
        private static readonly SaveProcessData ProcessData = new(new ProcessData());

        /// <summary>
        /// Состояние исполняемой операции; <see cref="SaveControllerStates.Idle"/> — очередь пуста
        /// </summary>
        public static SaveControllerStates State { get; private set; }

        protected override void OnDriverConnect()
        {
            Driver.SetIndexLink(SaveDataIndex);
        }

        protected override void OnDriverDisconnect()
        {
            //Ignore
        }

        /// <summary>
        /// Сохранение в очередь. Если GUID не указан — сохранится под новым GUID.
        /// Ожидающее сохранение того же GUID сливается с этим (имя и метаданные — от последнего запроса).
        /// Возвращает GUID записанного сейва; null — отклонено (в очереди или исполняется загрузка), отменено
        /// удалением этого сейва или не удалось.
        /// </summary>
        /// <param name="name">Название для сейва</param>
        /// <param name="guid">GUID перезаписываемого сейва; null — новый</param>
        public static UniTask<string> Save(string name, string guid = null) => EnqueueSave(name, guid);

        /// <summary>
        /// Загрузка в очередь. Повтор для того же GUID, пока он ожидает или исполняется, сливается с ним.
        /// Завершается после загрузки.
        /// </summary>
        /// <param name="guid">guid сейва</param>
        public static UniTask Load(string guid) => EnqueueLoad(guid);

        /// <summary>
        /// Удаление в очередь. Отменяет ожидающие сохранения этого GUID; повтор сливается. Завершается после
        /// удаления.
        /// </summary>
        public static UniTask Remove(string guid) => EnqueueRemove(guid);

        /// <summary>
        /// Возвращает номер-инкремент последнего сейва
        /// Индексация идет непрерывно
        /// </summary>
        /// <returns></returns>
        public static int GetNumberLastSave() => Driver.GetNumberLastSave();

        /// <summary>
        /// Получить JSON данные по ключу
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public static Dictionary<string, string> GetData(string id)
        {
            if (SaveDataIndex.TryGetValue(id, out var data))
                return data;
            Log.Print(new LogData(LogLevel.Error, $"Save data not found for id: {id}", Instance));
            return new Dictionary<string, string>();
        }

        public static Dictionary<string, SaveSummary> GetIndex() => Driver.GetIndex();

        /// <summary>
        /// Записать метаблок. В обработчике <see cref="OnSaveRequest"/> — в метаданные запроса (попадут в сводку при
        /// исполнении); в обработчике <see cref="OnSaveStart"/> — прямо в сводку исполняемого сохранения. Вне этих
        /// окон запись сбросится следующим циклом. Выключенные в настройках блоки драйвер отбрасывает, драйвер без
        /// поддержки метаданных вызов игнорирует.
        /// </summary>
        /// <param name="key">Ключ метаблока</param>
        /// <param name="metadata">Значение</param>
        public static void AddMeta(string key, string metadata)
        {
            if (_requestMeta != null)
            {
                _requestMeta[key] = metadata;
                return;
            }

            Driver.AddMeta(key, metadata);
        }

        public static void Register(ISaveable controller) => Saveables.Add(controller);

        public static void UnRegister(ISaveable controller) => Saveables.Remove(controller);

        public static SaveProcessData GetProcessData() => ProcessData;
    }
}
