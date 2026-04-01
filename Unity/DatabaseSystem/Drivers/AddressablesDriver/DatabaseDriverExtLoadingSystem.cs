using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
#if ENABLE_ADDRESSABLES
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
#endif
using Vortex.Core.DatabaseSystem.Bus;
using Vortex.Core.LoaderSystem.Bus;
using Vortex.Core.SettingsSystem.Bus;
using Vortex.Core.System.ProcessInfo;
using Vortex.Unity.DatabaseSystem.Presets;

namespace Vortex.Unity.DatabaseSystem.Drivers.AddressablesDriver

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
#if ENABLE_ADDRESSABLES
            var labels = Settings.Data().DatabaseLabels;
            if (labels == null || labels.Length == 0)
            {
                Debug.LogError(
                    "[DatabaseDriver] Метки (лейблы) не заданы в DatabaseSettings. Ассеты базы данных должны быть типа Addressable и помечены соответствующей меткой. Эти метки необходимо указать в DatabaseSettings.");
                return;
            }

            var handles = new List<AsyncOperationHandle<IList<IRecordPreset>>>();
            var allRecords = new List<IRecordPreset>();

            try
            {
                foreach (var label in labels)
                {
                    var handle = Addressables.LoadAssetsAsync<IRecordPreset>(label, null);
                    handles.Add(handle);
                }

                _processData.Size = handles.Count + 1;
                _processData.Progress = 0;

                foreach (var handle in handles)
                {
                    if (cancellationToken.IsCancellationRequested)
                        break;

                    var records = await handle.Task;
                    allRecords.AddRange(records);
                    _processData.Progress++;

                    await UniTask.Yield();
                }

                foreach (var data in allRecords)
                {
                    if (data is null) continue;
                    DatabaseDriverBase.PutData(data);
                }

                _processData.Progress++;

                CallOnInit();
            }
            finally
            {
                foreach (var handle in handles)
                    Addressables.Release(handle);
            }
#endif
        }

        public Type[] WaitingFor() => Type.EmptyTypes;
    }
}