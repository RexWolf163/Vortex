namespace AppScripts.CharacterViewSystem.Models
{
    /// <summary>
    /// Класс персонажа неуправляемого игроком
    /// </summary>
    public partial class NonPlayableCharacter : BaseCharacterModel<NonPlayableCharacter>
    {
        /// <summary>
        /// Метод запирающий все реактивные контейнеры на ключ.
        /// TODO сделан про запас
        /// </summary>
        /// <param name="key"></param>
        public void SetOwner(object key)
        {
        }
    }
}