using System;

namespace Vortex.Sdk.CharacterViewSystem.Abstractions
{
    [Serializable]
    public abstract class CharacterBehavior : ICharacterBehavior
    {
        /// <summary>
        /// Блок инициализации.
        /// В нем необходимо прописать условия запуска и остановки модели поведения
        /// </summary>
        public abstract void Init();

        /// <summary>
        /// Отписки и высвобождение ресурсов
        /// </summary>
        public abstract void Dispose();

        /// <summary>
        /// Запуск модели поведения персонажа
        /// </summary>
        public abstract void Run();

        /// <summary>
        /// Остановка выполнения действия для персонажа
        /// </summary>
        public abstract void Stop();
    }
}