using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Vortex.Core.DatabaseSystem.Bus;
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
            foreach (var resource in _resources)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    await UniTask.CompletedTask;
                    return;
                }

                _processData.Progress++;
                if (resource is not IRecordPreset data)
                    continue;
                DatabaseDriverBase.PutData(data);

                await UniTask.Yield();
            }

            CallOnInit();
#if !UNITY_EDITOR
            _resources = null;
#endif
            await UniTask.CompletedTask;
        }

        public Type[] WaitingFor() => Type.EmptyTypes;
    }
}