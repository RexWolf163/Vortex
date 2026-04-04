using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Vortex.Core.Extensions.LogicExtensions;
using Vortex.Core.LoggerSystem.Bus;
using Vortex.Core.LoggerSystem.Model;
using Vortex.Core.SaveSystem.Abstraction;
using Vortex.Core.SaveSystem.Model;
using Vortex.Core.System.Abstractions;
using Vortex.Core.System.ProcessInfo;

namespace Vortex.Core.SaveSystem.Bus
{
    public class SaveController : SystemController<SaveController, IDriver>
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
        /// Событие начала сохранения
        /// </summary>
        public static event Action OnLoadStart;

        /// <summary>
        /// Событие начала загрузки
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
        /// Текущее состояние контроллера, что загрузка идет
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
        /// Запуск процедуры сохранения данных
        /// Если GUID не указан - сохранится под новым GUID
        /// </summary>
        /// <param name="name">Название для сейва</param>
        /// <param name="guid"></param>
        public static async UniTask<string> Save(string name, string guid = null)
        {
            //Замок от перезапуска
            if (State == SaveControllerStates.Saving)
                return null;
            State = SaveControllerStates.Saving;
            OnSaveStart?.Invoke();
            try
            {
                SaveDataIndex.Clear();
                ProcessData.Global.Name = State.ToString();
                ProcessData.Global.Progress = 0;
                ProcessData.Global.Size = Saveables.Count;
                foreach (var saveable in Saveables)
                {
                    ProcessData.Global.Progress++;
                    ProcessData.Module = saveable.GetProcessInfo();
                    var data = await saveable.GetSaveData(Token);
                    SaveDataIndex.AddNew(saveable.GetSaveId(), data);
                }

                guid ??= Crypto.GetNewGuid();
                Driver.Save(name, guid);
            }
            catch (Exception e)
            {
                Log.Print(new LogData(LogLevel.Error, $"Error while saving data\n{e.Message}", "SaveController"));
            }

            State = SaveControllerStates.Idle;
            OnSaveComplete?.Invoke();
            return guid;
        }

        /// <summary>
        /// Загрузить сейв
        /// </summary>
        /// <param name="guid">guid сейва</param>
        public static async void Load(string guid)
        {
            State = SaveControllerStates.Loading;
            OnLoadStart?.Invoke();
            try
            {
                Driver.Load(guid);
                ProcessData.Global.Name = State.ToString();
                ProcessData.Global.Progress = 0;
                ProcessData.Global.Size = Saveables.Count;
                foreach (var saveable in Saveables)
                {
                    ProcessData.Global.Progress++;
                    ProcessData.Module = saveable.GetProcessInfo();
                    await saveable.OnLoad(Token);
                }
            }
            catch (Exception e)
            {
                Log.Print(new LogData(LogLevel.Error, $"Error while loading data\n{e.Message}\n{e.StackTrace}",
                    "SaveController"));
            }

            State = SaveControllerStates.Idle;
            OnLoadComplete?.Invoke();
        }

        /// <summary>
        /// Удалить сейв по ID
        /// </summary>
        public static void Remove(string guid)
        {
            try
            {
                Driver.Remove(guid);
                OnRemove?.Invoke();
            }
            catch (Exception e)
            {
                Log.Print(new LogData(LogLevel.Error, $"Error while loading data\n{e.Message}", "SaveController"));
            }
        }

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

        public static void Register(ISaveable controller) => Saveables.Add(controller);

        public static void UnRegister(ISaveable controller) => Saveables.Remove(controller);

        public static SaveProcessData GetProcessData() => ProcessData;
    }
}