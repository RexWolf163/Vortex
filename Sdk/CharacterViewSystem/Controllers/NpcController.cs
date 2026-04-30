using Vortex.Sdk.CharacterViewSystem.Models;

namespace Vortex.Sdk.CharacterViewSystem.Controllers
{
    /// <summary>
    /// Контроллер логики управления персонажем на карте
    /// </summary>
    public static partial class NpcController
    {
        /// <summary>
        /// Ключ для контейнеров
        /// </summary>
        private static readonly object Key = new();

        /// <summary>
        /// Записывает данную модель за этим контроллером, закрывая ее контейнеры
        /// </summary>
        /// <param name="npc"></param>
        public static void Lock(this NonPlayableCharacter npc) => npc.SetOwner(Key);
    }
}