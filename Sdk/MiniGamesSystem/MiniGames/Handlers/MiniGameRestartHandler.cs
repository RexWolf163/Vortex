using Sirenix.OdinInspector;
using UnityEngine;
using Vortex.Core.System.Abstractions;
using Vortex.Sdk.MiniGamesSystem.MiniGames.Abstractions;
using Vortex.Sdk.MiniGamesSystem.MiniGames.Model;
using Vortex.Unity.EditorTools.Attributes;

namespace Vortex.Sdk.MiniGamesSystem.MiniGames.Handlers
{
    public class MiniGameRestartHandler : MonoBehaviour
    {
        [SerializeField, ClassFilter(typeof(IDataStorage))]
        private MonoBehaviour storage;

        private IDataStorage source;

        private IDataStorage Source => source ??= storage as IDataStorage;

        private IMiniGameController<MiniGameData> data;
        private IMiniGameController<MiniGameData> Data => data ??= Source.GetData<IMiniGameController<MiniGameData>>();

        [Button("Restart Game")]
        public void RestartMiniGame() => Data.Play();
    }
}