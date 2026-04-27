using UnityEngine;
using Vortex.Core.Extensions.ReactiveValues;

namespace Vortex.Sdk.MapLevels.Model
{
    /// <summary>
    /// Враппер per-level: держит ссылку на инстанс префаба уровня и его текущее состояние загрузки.
    /// </summary>
    public sealed class MapContainer
    {
        public string Guid { get; }

        public ReactiveValue<MapContainerState> State { get; } = new MapContainerStateData(MapContainerState.Empty);

        public GameObject Instance { get; internal set; }

        public MapContainer(string guid)
        {
            Guid = guid;
        }

        /// <summary>
        /// Реактивная обёртка для MapContainerState.
        /// </summary>
        private sealed class MapContainerStateData : ReactiveValue<MapContainerState>
        {
            public MapContainerStateData(MapContainerState value) => Value = value;
        }
    }
}
