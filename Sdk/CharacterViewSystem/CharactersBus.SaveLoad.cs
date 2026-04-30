using System.Collections.Generic;
using System.Threading;
using Vortex.Sdk.CharacterViewSystem.Controllers;
using Vortex.Sdk.CharacterViewSystem.Models;
using Cysharp.Threading.Tasks;
using Vortex.Core.DatabaseSystem.Bus;
using Vortex.Core.Extensions.LogicExtensions;
using Vortex.Core.Extensions.LogicExtensions.SerializationSystem;
using Vortex.Core.SaveSystem;
using Vortex.Core.SaveSystem.Bus;
using Vortex.Core.System.ProcessInfo;

namespace Vortex.Sdk.CharacterViewSystem
{
    public partial class CharactersBus : ISaveable
    {
        private const byte BatchSize = 20;

        private const string SaveKey = "NpcData";
        private const string PcIndexKey = "pcIndex";

        private static readonly ProcessData ProcessData = new ProcessData()
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
            ProcessData.Size = NpcIndex.Count;
            ProcessData.Progress = 0;

            foreach (var (presetId, index) in NpcIndex)
            {
                var str = index.SerializeProperties().Compress();
                result.AddNew(presetId, str);
                counter += index.Count + 1;
                if (counter >= BatchSize)
                    await UniTask.Yield();
                ProcessData.Progress++;
            }

            await UniTask.Yield();
            return result;
        }

        public ProcessData GetProcessInfo() => ProcessData;

        public async UniTask OnLoad(CancellationToken cancellationToken)
        {
            var data = SaveController.GetData(SaveKey);

            // — PC Index —
            PcIndex.Clear();
            var list = Database.GetRecords<PlayableCharacter>();
            foreach (var record in list)
            {
                record.Lock();
                PcIndex.AddNew(record.GuidPreset, record);
            }

            await UniTask.Yield();

            // — NPC Index —
            NpcIndex.Clear();
            foreach (var (presetId, savedData) in data)
            {
                if (presetId == PcIndexKey) continue;

                var json = savedData.Decompress();
                var npcDict = json.DeserializeProperties<Dictionary<string, NonPlayableCharacter>>();
                if (npcDict == null) continue;

                var restored = new Dictionary<string, NonPlayableCharacter>();
                foreach (var (charId, npc) in npcDict)
                {
                    if (npc == null) continue;
                    npc.Lock();
                    restored.Add(charId, npc);
                }

                NpcIndex.Add(presetId, restored);
            }

            await UniTask.Yield();
        }
    }
}