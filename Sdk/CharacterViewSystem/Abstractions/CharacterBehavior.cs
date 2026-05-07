using System;
using Sirenix.OdinInspector;

namespace Vortex.Sdk.CharacterViewSystem.Abstractions
{
    [Serializable]
    public abstract class CharacterBehavior
    {
        [DisplayAsString, ShowInInspector, HideLabel, PropertyOrder(-100)]
        private string Name => GetType().Name;

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