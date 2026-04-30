using Vortex.Sdk.CharacterViewSystem.Models;

namespace Vortex.Sdk.CharacterViewSystem.Controllers
{
    /// <summary>
    /// Контроллер логики управления персонажем на карте
    /// </summary>
    public static partial class PcController
    {
        /// <summary>
        /// Ключ для контейнеров
        /// </summary>
        private static readonly object Key = new();

        /// <summary>
        /// Записывает данную модель за этим контроллером, закрывая ее контейнеры
        /// </summary>
        /// <param name="pc"></param>
        public static void Lock(this PlayableCharacter pc) => pc.SetOwner(Key);

        /// <summary>
        /// Установить значение доступности персонажа для игрока 
        /// </summary>
        /// <param name="pc">Указанный персонаж</param>
        /// <param name="b"></param>
        internal static void SetOwnedValue(this PlayableCharacter pc, bool b) => pc.IsOwned.Set(b, Key);
    }
}