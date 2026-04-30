using Vortex.Core.Extensions.ReactiveValues;

namespace AppScripts.CharacterViewSystem.Models
{
    /// <summary>
    /// Класс персонажа управляемого игроком
    /// </summary>
    public partial class PlayableCharacter : BaseCharacterModel<PlayableCharacter>
    {
        /// <summary>
        /// Персонаж активен (управляется игроком)
        /// </summary>
        public BoolData IsActive { get; internal set; } = new(false);

        /// <summary>
        /// Признак владения персонажем.
        /// Если TRUE, персонаж "выкуплен" или иначе привязан к игроку
        /// </summary>
        public BoolData IsOwned { get; } = new(false);

        /// <summary>
        /// Закрытие ключом всех контейнеров
        /// </summary>
        /// <param name="key"></param>
        public void SetOwner(object key)
        {
            IsActive.SetOwner(key);
            IsOwned.SetOwner(key);
        }
    }
}