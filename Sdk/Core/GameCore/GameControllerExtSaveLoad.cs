using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Vortex.Core.SaveSystem;
using Vortex.Core.SaveSystem.Bus;
using Vortex.Core.System.ProcessInfo;

namespace Vortex.Sdk.Core.GameCore
{
    public partial class GameController : ISaveable
    {
        /// <summary>
        /// Завершение загрузки
        /// </summary>
        public static event Action OnLoadGame;

        private const string SaveKey = "GameSave";

        private static readonly ProcessData ProcessData = new(name: SaveKey);

        public string GetSaveId() => SaveKey;

        public async UniTask<Dictionary<string, string>> GetSaveData(CancellationToken cancellationToken)
        {
            var data = new Dictionary<string, string>() { { "data", Serialize() } };
            await UniTask.Yield();
            return data;
        }

        public ProcessData GetProcessInfo() => ProcessData;

        public async UniTask OnLoad(CancellationToken cancellationToken)
        {
            _newGameLock = true;
            SetGameState(GameStates.Off);
            await UniTask.Yield();
            SetGameState(GameStates.Loading);

            var slot = SaveController.GetData(SaveKey);
            if (!slot.TryGetValue("data", out var data))
                return;
            ProcessData.Progress = 0;
            ProcessData.Size = 1;
            Deserialize(data);
            ProcessData.Progress++;
            await UniTask.Yield();

            _data.Init();
            SetGameState(GameStates.Play);
            OnLoadGame?.Invoke();
        }
    }
}