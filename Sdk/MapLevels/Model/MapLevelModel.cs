using System;
using UnityEngine;
using Vortex.Core.DatabaseSystem.Model;
using Vortex.Core.Extensions.LogicExtensions;
using Vortex.Core.Extensions.LogicExtensions.SerializationSystem;

namespace Vortex.Sdk.MapLevels.Model
{
    /// <summary>
    /// Рабочая копия данных одного уровня карты.
    /// Заполняется из MapLevelPreset через Record.CopyFrom при инициализации БД.
    /// </summary>
    [Serializable, POCO]
    public sealed class MapLevelModel : Record
    {
        /// <summary>
        /// Префаб уровня. Прямая ссылка GameObject — стриминг через Addressables не используется.
        /// </summary>
        [NotPOCO]
        public GameObject Prefab { get; internal set; }

        /// <summary>
        /// GUID-и соседних уровней по графу карты.
        /// </summary>
        public string[] NeighborGuids { get; internal set; }

        public override string GetDataForSave() => this.SerializeProperties();

        public override void LoadFromSaveData(string data) =>
            this.CopyFrom(data.DeserializeProperties<MapLevelModel>());
    }
}
