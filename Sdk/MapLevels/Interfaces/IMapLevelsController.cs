using System;
using Vortex.Sdk.MapLevels.Model;

namespace Vortex.Sdk.MapLevels.Interfaces
{
    /// <summary>
    /// Контракт контроллера пакета MapLevels.
    /// Публичный API синхронный — async-операции (предзагрузка соседей)
    /// инкапсулированы внутри реализации и наружу не торчат.
    /// </summary>
    public interface IMapLevelsController
    {
        /// <summary>
        /// Завершена ли инициализация контроллера.
        /// </summary>
        bool IsInitialized { get; }

        /// <summary>
        /// Runtime-модель пакета.
        /// </summary>
        MapLevelsModel Model { get; }

        /// <summary>
        /// Контроллер инициализирован, каталог загружен, готов к работе.
        /// </summary>
        event Action OnInitialized;

        /// <summary>
        /// Активный уровень изменился. Аргумент — новый GUID.
        /// </summary>
        event Action<string> OnActiveLevelChanged;

        /// <summary>
        /// Префаб уровня инстанцирован.
        /// </summary>
        event Action<string> OnLevelLoaded;

        /// <summary>
        /// Инстанс уровня уничтожен.
        /// </summary>
        event Action<string> OnLevelUnloaded;

        /// <summary>
        /// Инициализация контроллера. Читает параметры из Settings.Data(),
        /// загружает каталог из Database, подписывается на жизненный цикл игры.
        /// Идемпотентна.
        /// </summary>
        void Init();

        /// <summary>
        /// Активировать уровень. Если префаб ещё не инстанцирован — инстанцирует синхронно,
        /// затем переключает активный. Подгрузка соседей (hop=1) запускается в фоне.
        /// </summary>
        void Enter(string levelGuid);

        /// <summary>
        /// Освобождение всех инстансов и снятие подписок. Идемпотентна.
        /// </summary>
        void Cleanup();
    }
}
