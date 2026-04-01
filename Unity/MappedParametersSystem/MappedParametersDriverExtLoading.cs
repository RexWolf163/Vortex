using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Vortex.Core.Extensions.LogicExtensions;
using Vortex.Core.LoaderSystem.Bus;
using Vortex.Core.MappedParametersSystem.Base;
using Vortex.Core.MappedParametersSystem.Bus;
using Vortex.Core.System.ProcessInfo;
using Vortex.Unity.MappedParametersSystem.Base.Preset;

namespace Vortex.Unity.MappedParametersSystem
{
    public partial class MappedParametersDriver : IProcess
    {
        private ProcessData _processData;

        public ProcessData GetProcessInfo() => _processData;


        [RuntimeInitializeOnLoadMethod]
        private static void Register()
        {
            if (!ParameterMaps.SetDriver(Instance))
            {
                Dispose();
                return;
            }

            Loader.Register(Instance);
        }


        public async UniTask RunAsync(CancellationToken cancellationToken)
        {
            _processData = new ProcessData
            {
                Name = "Mapped Parameters",
                Progress = 1,
                Size = 1
            };

            var resources = Resources.LoadAll<ParametersMapStorage>("");
            _indexMaps.Clear();

            _processData.Size = resources.Length;
            var c = 0;
            foreach (var map in resources)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    await UniTask.CompletedTask;
                    return;
                }

                _processData.Progress++;
                _indexMaps.AddNew(map.guid, GetMap(map));

                if (++c < 100)
                    continue;
                c = 0;
                await UniTask.Yield();
            }

            await UniTask.CompletedTask;
        }

        public Type[] WaitingFor() => null;

        private ParametersMap GetMap(ParametersMapStorage mapAsset)
        {
            var mappedParameters = new IParameterMap[mapAsset.baseParams.Length + mapAsset.mappedParams.Length];

            var c = mapAsset.baseParams.Length;
            for (var i = 0; i < c; i++)
            {
                var param = mapAsset.baseParams[i];
                mappedParameters[i] = new MappedParameterPreset(param);
            }

            for (var i = mapAsset.mappedParams.Length - 1 + c; i >= c; i--)
            {
                var param = mapAsset.mappedParams[i - c];
                mappedParameters[i] = param;
            }

            return new ParametersMap(mapAsset.guid, mappedParameters);
        }
    }
}