using System;
using UnityEngine;
using Vortex.Core.DatabaseSystem.Bus;
using Vortex.Core.DatabaseSystem.Model.Enums;
using Vortex.Sdk.MapLevels.Model;
using Vortex.Unity.DatabaseSystem.Attributes;
using Vortex.Unity.DatabaseSystem.Presets;

namespace Vortex.Sdk.MapLevels.Presets
{
    /// <summary>
    /// Пресет уровня карты. Singleton-Record (один пресет = одна runtime-копия).
    /// Хранит прямую ссылку на префаб и GUID-список соседей.
    /// </summary>
    [CreateAssetMenu(fileName = "MapLevel", menuName = "Database/MapLevel Preset")]
    [Serializable]
    public sealed class MapLevelPreset : RecordPreset<MapLevelModel>
    {
        [SerializeField] private GameObject prefab;

        [SerializeField, DbRecord(typeof(MapLevelModel), RecordTypes.Singleton)]
        private string[] neighborGuids;

        public GameObject Prefab => prefab;
        public string[] NeighborGuids => neighborGuids;

        /// <summary>
        /// Удобный аксессор соседей через Database.
        /// Корректен только потому, что MapLevelPreset фиксирован как Singleton.
        /// Для MultiInstance такая выборка по GUID была бы семантически неоднозначной.
        /// </summary>
        public MapLevelModel[] Neighbors
        {
            get
            {
                if (neighborGuids == null || neighborGuids.Length == 0)
                    return Array.Empty<MapLevelModel>();

                var result = new MapLevelModel[neighborGuids.Length];
                for (var i = 0; i < neighborGuids.Length; i++)
                    result[i] = Database.GetRecord<MapLevelModel>(neighborGuids[i]);
                return result;
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (type != RecordTypes.Singleton)
            {
                type = RecordTypes.Singleton;
                Debug.LogWarning(
                    $"[MapLevelPreset] {name}: тип записи принудительно установлен в Singleton. " +
                    "Уровни карты не должны размножаться.");
            }
        }
#endif
    }
}