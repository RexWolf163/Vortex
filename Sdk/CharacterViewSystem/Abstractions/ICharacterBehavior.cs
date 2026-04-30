using System;

namespace AppScripts.CharacterViewSystem.Abstractions
{
    /// <summary>
    /// Интерфейс модели поведения персонажа
    /// </summary>
    public interface ICharacterBehavior : IDisposable
    {
        /// <summary>
        /// Блок инициализации.
        /// В нем необходимо прописать условия запуска и остановки модели поведения
        /// </summary>
        public void Init();

        /// <summary>
        /// Запуск модели поведения персонажа
        /// </summary>
        public void Run();

        /// <summary>
        /// Остановка выполнения действия для персонажа
        /// </summary>
        public void Stop();
    }
}