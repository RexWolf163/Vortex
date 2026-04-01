using UnityEngine;
using Vortex.Sdk.MiniGamesSystem.MiniGames.Abstractions;
using Vortex.Unity.EditorTools.Attributes;

namespace Vortex.Sdk.MiniGamesSystem.MiniGames.Handlers
{
    public class MiniGameCheatFailHandler : MonoBehaviour
    {
        [SerializeField, ClassFilter(typeof(IMiniGameHub))]
        private MonoBehaviour hub;

        public void CheatFail() => ((IMiniGameHub)hub).GetController().CheatFail();
    }
}