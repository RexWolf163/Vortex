using Naninovel;
using UnityEngine;
using Vortex.Sdk.Core.GameCore;

namespace Vortex.NaniExtensions.Core
{
    /// <summary>
    /// Регистрирует Naninovel в GameController как сервис игровой сессии.
    /// Перенесено из жёсткой ссылки внутри Sdk/Core: теперь Sdk/Core
    /// не знает о Naninovel, а ожидание Engine.Initialized живёт здесь.
    /// </summary>
    public sealed class NaniSessionService : IGameSessionService
    {
        public bool IsReady => Engine.Initialized;
        public string Name => "Naninovel.Engine";

        [RuntimeInitializeOnLoadMethod]
        private static void Register()
            => GameController.RegisterSessionService(new NaniSessionService());
    }
}
