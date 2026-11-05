using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Vortex.Core.Extensions.LogicExtensions;
using Vortex.Core.LoggerSystem.Bus;
using Vortex.Core.LoggerSystem.Model;
using Vortex.Core.SaveSystem.Model;

namespace Vortex.Core.SaveSystem.Bus
{
    /// <summary>
    /// Очередь операций слотов. Операции исполняются строго по одной в порядке постановки (FIFO); следующая
    /// начинается после завершения текущей. Батчинг — по последней операции того же GUID в очереди:
    /// <list type="bullet">
    /// <item>Save того же GUID, ещё не начатый, — слияние: имя и метаданные от нового запроса, результат общий;</item>
    /// <item>Load того же GUID — слияние, только если он последний во всей очереди (ожидающий или исполняемый):
    /// загрузка меняет состояние всей игры, перестановка относительно других загрузок недопустима;</item>
    /// <item>Remove того же GUID, ожидающий или исполняемый, — слияние;</item>
    /// <item>Remove отменяет ожидающие Save этого GUID, стоящие после последней его загрузки (результат — null);</item>
    /// <item>Save при исполняемой или ожидающей загрузке отклоняется: после неё состояние уже другое.</item>
    /// </list>
    /// Новый сейв (GUID не задан) не сливается ни с чем.
    /// </summary>
    public partial class SaveController
    {
        private enum OperationType
        {
            Save,
            Load,
            Remove
        }

        /// <summary>
        /// Операция очереди. Результат общий для всех слитых запросов: GUID — успех, null — отказ/отмена/сбой.
        /// </summary>
        private sealed class Operation
        {
            public readonly OperationType Type;

            /// <summary>GUID сейва; у нового сохранения null до исполнения</summary>
            public readonly string Guid;

            public readonly UniTaskCompletionSource<string> Completion = new();

            /// <summary>Имя сейва (Save)</summary>
            public string Name;

            /// <summary>Метаданные момента запроса (Save)</summary>
            public Dictionary<string, string> Meta;

            public Operation(OperationType type, string guid)
            {
                Type = type;
                Guid = guid;
            }
        }

        private static readonly LinkedList<Operation> Pending = new();

        /// <summary>
        /// Исполняемая операция; null — ничего не исполняется
        /// </summary>
        private static Operation _current;

        /// <summary>
        /// Цикл обработки очереди запущен: новые операции подхватит он же
        /// </summary>
        private static bool _isProcessing;

        /// <summary>
        /// Метаданные запроса на время <see cref="OnSaveRequest"/>; null — вне окна запроса
        /// </summary>
        private static Dictionary<string, string> _requestMeta;

        #region Enqueue

        private static UniTask<string> EnqueueSave(string name, string guid)
        {
            if (_requestMeta != null)
            {
                Log.Print(new LogData(LogLevel.Error,
                    $"Save «{name}» из обработчика OnSaveRequest отклонён", "SaveController"));
                return UniTask.FromResult<string>(null);
            }

            if (HasLoad())
            {
                Log.Print(new LogData(LogLevel.Warning,
                    $"Save «{name}» отклонён: в очереди или исполняется загрузка", "SaveController"));
                return UniTask.FromResult<string>(null);
            }

            var meta = CollectRequestMeta();

            if (guid != null)
            {
                var last = FindLast(guid);
                if (last != null && last != _current && last.Type == OperationType.Save)
                {
                    last.Name = name;
                    last.Meta = meta;
                    return last.Completion.Task;
                }
            }

            return Enqueue(new Operation(OperationType.Save, guid) { Name = name, Meta = meta });
        }

        private static UniTask EnqueueLoad(string guid)
        {
            // Загрузка меняет состояние всей игры, а не одного сейва: сливать только с хвостом всей очереди.
            // Слияние с более ранней загрузкой того же GUID переставило бы порядок (A, B, A → итог B)
            var tail = Pending.Last?.Value ?? _current;
            if (tail != null && tail.Type == OperationType.Load && tail.Guid == guid)
                return tail.Completion.Task.AsUniTask();

            return Enqueue(new Operation(OperationType.Load, guid)).AsUniTask();
        }

        private static UniTask EnqueueRemove(string guid)
        {
            CancelTrailingSaves(guid);

            var last = FindLast(guid);
            if (last != null && last.Type == OperationType.Remove)
                return last.Completion.Task.AsUniTask();

            return Enqueue(new Operation(OperationType.Remove, guid)).AsUniTask();
        }

        private static UniTask<string> Enqueue(Operation operation)
        {
            Pending.AddLast(operation);
            var task = operation.Completion.Task;
            if (!_isProcessing)
                Process().Forget();
            return task;
        }

        /// <summary>
        /// Последняя операция по GUID: с конца очереди, затем исполняемая; null — по GUID ничего нет
        /// </summary>
        private static Operation FindLast(string guid)
        {
            for (var node = Pending.Last; node != null; node = node.Previous)
                if (node.Value.Guid == guid)
                    return node.Value;

            return _current != null && _current.Guid == guid ? _current : null;
        }

        /// <summary>
        /// Отменить ожидающие Save этого GUID с конца очереди до первой иной операции по нему (загрузки, удаления):
        /// запись перед удалением бессмысленна, а сохранение до загрузки определяет, что она загрузит.
        /// </summary>
        private static void CancelTrailingSaves(string guid)
        {
            var node = Pending.Last;
            while (node != null)
            {
                var previous = node.Previous;
                var operation = node.Value;
                if (operation.Guid == guid)
                {
                    if (operation.Type != OperationType.Save)
                        return;

                    Pending.Remove(node);
                    operation.Completion.TrySetResult(null);
                }

                node = previous;
            }
        }

        private static bool HasLoad()
        {
            if (_current?.Type == OperationType.Load)
                return true;

            foreach (var operation in Pending)
                if (operation.Type == OperationType.Load)
                    return true;
            return false;
        }

        /// <summary>
        /// Метаданные момента запроса: обработчики <see cref="OnSaveRequest"/> пишут их через <see cref="AddMeta"/>
        /// </summary>
        private static Dictionary<string, string> CollectRequestMeta()
        {
            var meta = new Dictionary<string, string>();
            _requestMeta = meta;
            try
            {
                Raise(OnSaveRequest, nameof(OnSaveRequest));
            }
            finally
            {
                _requestMeta = null;
            }

            return meta;
        }

        #endregion

        #region Process

        /// <summary>
        /// Цикл обработки: по одной операции до опустошения очереди. Операции, поставленные из событий и
        /// продолжений завершённых операций, подхватываются тем же циклом.
        /// </summary>
        private static async UniTaskVoid Process()
        {
            _isProcessing = true;
            try
            {
                while (Pending.Count > 0)
                {
                    var operation = Pending.First.Value;
                    Pending.RemoveFirst();
                    _current = operation;

                    string result;
                    switch (operation.Type)
                    {
                        case OperationType.Save:
                            result = await ExecuteSave(operation);
                            break;
                        case OperationType.Load:
                            result = await ExecuteLoad(operation);
                            break;
                        case OperationType.Remove:
                            result = ExecuteRemove(operation);
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }

                    _current = null;
                    if (Pending.Count == 0)
                        State = SaveControllerStates.Idle;

                    RaiseComplete(operation.Type, result);
                    operation.Completion.TrySetResult(result);
                }
            }
            finally
            {
                _current = null;
                _isProcessing = false;
                State = SaveControllerStates.Idle;
            }
        }

        private static async UniTask<string> ExecuteSave(Operation operation)
        {
            State = SaveControllerStates.Saving;
            try
            {
                // Новая сводка цикла: метаблоки прошлого цикла не переносятся; метаданные запроса — первыми
                Driver.BeginSummary();
                foreach (var (key, value) in operation.Meta)
                    Driver.AddMeta(key, value);
                Raise(OnSaveStart, nameof(OnSaveStart));

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

                var guid = operation.Guid ?? Crypto.GetNewGuid();
                Driver.Save(operation.Name, guid);
                return guid;
            }
            catch (Exception e)
            {
                Log.Print(new LogData(LogLevel.Error, $"Error while saving data\n{e.Message}", "SaveController"));
                return null;
            }
        }

        private static async UniTask<string> ExecuteLoad(Operation operation)
        {
            State = SaveControllerStates.Loading;
            Raise(OnLoadStart, nameof(OnLoadStart));
            try
            {
                Driver.Load(operation.Guid);
                ProcessData.Global.Name = State.ToString();
                ProcessData.Global.Progress = 0;
                ProcessData.Global.Size = Saveables.Count;
                foreach (var saveable in Saveables)
                {
                    ProcessData.Global.Progress++;
                    ProcessData.Module = saveable.GetProcessInfo();
                    await saveable.OnLoad(Token);
                }

                return operation.Guid;
            }
            catch (Exception e)
            {
                Log.Print(new LogData(LogLevel.Error, $"Error while loading data\n{e.Message}\n{e.StackTrace}",
                    "SaveController"));
                return null;
            }
        }

        private static string ExecuteRemove(Operation operation)
        {
            try
            {
                Driver.Remove(operation.Guid);
                return operation.Guid;
            }
            catch (Exception e)
            {
                Log.Print(new LogData(LogLevel.Error, $"Error while removing data\n{e.Message}", "SaveController"));
                return null;
            }
        }

        /// <summary>
        /// Событие завершения операции. Сохранение и загрузка сообщают о завершении и при сбое (как раньше),
        /// удаление — только при успехе.
        /// </summary>
        private static void RaiseComplete(OperationType type, string result)
        {
            switch (type)
            {
                case OperationType.Save:
                    Raise(OnSaveComplete, nameof(OnSaveComplete));
                    break;
                case OperationType.Load:
                    Raise(OnLoadComplete, nameof(OnLoadComplete));
                    break;
                case OperationType.Remove:
                    if (result != null)
                        Raise(OnRemove, nameof(OnRemove));
                    break;
            }
        }

        /// <summary>
        /// Вызов события с изоляцией подписчиков: исключение одного логируется и не мешает остальным и очереди
        /// </summary>
        private static void Raise(Action handlers, string eventName)
        {
            if (handlers == null)
                return;

            foreach (var handler in handlers.GetInvocationList())
            {
                try
                {
                    ((Action)handler).Invoke();
                }
                catch (Exception e)
                {
                    Log.Print(new LogData(LogLevel.Error,
                        $"Exception in {eventName} handler {handler.Method.DeclaringType?.Name}.{handler.Method.Name}\n" +
                        $"{e.Message}\n{e.StackTrace}", "SaveController"));
                }
            }
        }

        #endregion
    }
}
