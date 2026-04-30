using System.Collections.Generic;
using System.Threading;
using AppScripts.CharacterViewSystem.Models;
using Cysharp.Threading.Tasks;
using Vortex.Core.DatabaseSystem.Bus;
using Vortex.Core.Extensions.LogicExtensions;
using Vortex.Core.Extensions.LogicExtensions.SerializationSystem;
using Vortex.Core.SaveSystem;
using Vortex.Core.SaveSystem.Bus;
using Vortex.Core.System.ProcessInfo;

namespace AppScripts.CharacterViewSystem
{
    public partial class CharactersBus : ISaveable
    {
        private const byte BatchSize = 20;

        private const string SaveKey = "NpcData";
        private const string PcIndexKey = "pcIndex";
        private const char Divider = '\t';

        private static ProcessData _processData = new ProcessData()
        {
            Name = SaveKey,
            Progress = 0,
            Size = 0
        };

        public string GetSaveId() => SaveKey;

        public async UniTask<Dictionary<string, string>> GetSaveData(CancellationToken cancellationToken)
        {
            var result = new Dictionary<string, string>();
            var counter = 0;
            _processData.Size = NpcIndex.Count + 1;
            _processData.Progress = 0;

            foreach (var (presetId, index) in NpcIndex)
            {
                var str = index.SerializeProperties().Compress();
                result.AddNew(presetId, str);
                counter += index.Count + 1;
                if (counter >= BatchSize)
                    await UniTask.Yield();
                _processData.Progress++;
            }

            result.AddNew(PcIndexKey, string.Join(Divider, PcIndex.Keys));
            _processData.Progress++;

            await UniTask.Yield();
            return result;
        }

        public ProcessData GetProcessInfo() => _processData;

        public async UniTask OnLoad(CancellationToken cancellationToken)
        {
            var data = SaveController.GetData(SaveKey);
            PcIndex.Clear();

            var list = data[PcIndexKey].Split(Divider);
            foreach (var id in list)
            {
                PcIndex.AddNew(id, null);
            }

            await UniTask.Yield();

            NpcIndex.Clear();
        }
    }
}