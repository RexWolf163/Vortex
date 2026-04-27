using System;
using System.Collections.Generic;
using Vortex.Core.Extensions.ReactiveValues;

namespace Vortex.Sdk.MapLevels.Model
{
    /// <summary>
    /// Runtime-агрегатор состояния пакета MapLevels.
    /// Не сериализуется. Восстанавливается из первичных данных GameModel при загрузке.
    /// Параметры конфигурации читаются из Settings.Data() напрямую в контроллере, не хранятся в модели.
    /// </summary>
    public sealed class MapLevelsModel : IReactiveData
    {
        public event Action OnUpdateData;

        /// <summary>
        /// Каталог рабочих копий уровней. Заполняется контроллером при Init из Database.
        /// </summary>
        internal Dictionary<string, MapLevelModel> Catalog { get; set; } = new();

        /// <summary>
        /// Контейнеры уровней (per-level состояние загрузки и инстансы).
        /// </summary>
        internal Dictionary<string, MapContainer> Containers { get; set; } = new();

        /// <summary>
        /// GUID активного уровня (Empty = нет активного).
        /// </summary>
        public StringData ActiveLevelGuid { get; } = new(string.Empty);

        public IReadOnlyDictionary<string, MapLevelModel> GetCatalog() => Catalog;
        public IReadOnlyDictionary<string, MapContainer> GetContainers() => Containers;
        public MapLevelModel GetLevel(string guid) =>
            Catalog.TryGetValue(guid, out var level) ? level : null;
        public MapContainer GetContainer(string guid) =>
            Containers.TryGetValue(guid, out var container) ? container : null;

        internal void RaiseUpdate() => OnUpdateData?.Invoke();
    }
}
