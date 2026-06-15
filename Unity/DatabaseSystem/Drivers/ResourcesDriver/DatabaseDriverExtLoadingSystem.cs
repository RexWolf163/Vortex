using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Vortex.Core.DatabaseSystem.Bus;
using Vortex.Core.Extensions.LogicExtensions;
using Vortex.Core.LoaderSystem.Bus;
using Vortex.Core.System.ProcessInfo;
using Vortex.Unity.DatabaseSystem.Presets;

namespace Vortex.Unity.DatabaseSystem.Drivers.ResourcesDriver
{
    public partial class DatabaseDriver : IProcess
    {
        private const string Path = "Database";

        private ProcessData _processData = new()
        {
            Name = "Database",
            Progress = 0,
            Size = 0
        };

        /// <summary>
        /// Кешированный список ресурсов. Очищается после заполнения индексов
        /// </summary>
        private static UnityEngine.Object[] _resources;

        [RuntimeInitializeOnLoadMethod]
        private static void Register()
        {
            if (!Database.SetDriver(Instance))
            {
                Dispose();
                return;
            }

            Loader.Register(Instance);
        }

        public ProcessData GetProcessInfo() => _processData;

        public async UniTask RunAsync(CancellationToken cancellationToken)
        {
            DatabaseDriverBase.Clean();

            _resources = Resources.LoadAll(Path);
            _processData.Size = _resources.Length;

            const double frameBudgetMs = 8.0; // ~полкадра при 60fps
            var sw = System.Diagnostics.Stopwatch.StartNew();

            foreach (var resource in _resources)
            {
                if (cancellationToken.IsCancellationRequested)
                    return;

                _processData.Progress++;
                if (resource is IRecordPreset data)
                    DatabaseDriverBase.PutData(data);

                if (sw.Elapsed.TotalMilliseconds >= frameBudgetMs)
                {
                    await UniTask.Yield();
                    sw.Restart();
                }
            }

            CallOnInit();
#if !UNITY_EDITOR
    _resources = null;
#endif
        }

        public Type[] WaitingFor() => Type.EmptyTypes;
    }
}