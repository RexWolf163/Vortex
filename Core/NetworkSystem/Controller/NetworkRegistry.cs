using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Vortex.Core.NetworkSystem
{
    /// <summary>
    /// Реестр активных запросов: все запущенные и не забранные владельцем. Очередь ожидающих слот (FIFO) и счётчик
    /// выполняющихся — для лимита одновременных запросов. Снаружи только чтение и события; меняет контроллер.
    /// </summary>
    public sealed class NetworkRegistry
    {
        private readonly List<Query> _active = new();
        private readonly LinkedList<UniTaskCompletionSource> _waiting = new();

        /// <summary>Запущенные и не освобождённые запросы в порядке запуска.</summary>
        public IReadOnlyList<Query> Active => _active;

        /// <summary>Сколько запросов выполняет попытку сейчас.</summary>
        public int RunningCount { get; private set; }

        /// <summary>Сколько запросов ждёт слот.</summary>
        public int QueuedCount => _waiting.Count;

        public event Action<Query> OnAdded;
        public event Action<Query> OnRemoved;

        internal void Add(Query query)
        {
            _active.Add(query);
            OnAdded?.Invoke(query);
        }

        internal void Remove(Query query)
        {
            if (_active.Remove(query))
                OnRemoved?.Invoke(query);
        }

        /// <summary>Занять слот: сразу, если есть свободный и очередь пуста, иначе — в порядке очереди.</summary>
        internal async UniTask AcquireSlotAsync(int limit, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (RunningCount < limit && _waiting.Count == 0)
            {
                RunningCount++;
                return;
            }

            var source = new UniTaskCompletionSource();
            var node = _waiting.AddLast(source);
            using (token.Register(() =>
                   {
                       if (node.List == null)
                           return;
                       _waiting.Remove(node);
                       source.TrySetCanceled(token);
                   }))
            {
                await source.Task;
            }
        }

        /// <summary>Вернуть слот и передать освободившиеся слоты ожидающим.</summary>
        internal void ReleaseSlot(int limit)
        {
            RunningCount--;
            while (RunningCount < limit && _waiting.Count > 0)
            {
                var next = _waiting.First.Value;
                _waiting.RemoveFirst();
                RunningCount++;
                next.TrySetResult();
            }
        }
    }
}
