using Vortex.Core.System.Abstractions;
using Vortex.Sdk.MapLevels.Interfaces;
using Vortex.Sdk.MapLevels.Model;

namespace Vortex.Sdk.MapLevels.Controllers
{
    /// <summary>
    /// Узловой контроллер пакета MapLevels.
    /// Реализация по умолчанию — реальный лайфцикл, без моков.
    ///
    /// Разделён на partial-файлы по темам:
    ///   - MapLevelsController.cs           — Singleton-каркас, поля
    ///   - MapLevelsController.Lifecycle.cs — Init / Cleanup, привязка к GameController и Bus
    ///   - MapLevelsController.Active.cs    — Enter, переключение активного уровня
    ///   - MapLevelsController.Streaming.cs — план соседей, инстанциация и выгрузка
    /// </summary>
    public partial class MapLevelsController : Singleton<MapLevelsController>, IMapLevelsController
    {
        public bool IsInitialized { get; private set; }
        public MapLevelsModel Model { get; private set; }
    }
}
