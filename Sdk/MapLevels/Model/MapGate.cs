using System;
using System.Collections.Generic;
using System.Linq;
using Sirenix.OdinInspector;
using Sirenix.Utilities;
using UnityEngine;
using UnityEngine.Serialization;
using Vortex.Core.DatabaseSystem.Bus;
using Vortex.Unity.DatabaseSystem.Attributes;
using Vortex.Unity.EditorTools.Attributes;

namespace Vortex.Sdk.MapLevels.Model
{
    /// <summary>
    /// Точа спауна для карты
    /// </summary>
    [Serializable]
    public struct MapGate
    {
        private const string Divider = "||";

        /// <summary>
        /// Идентификатор
        /// </summary>
        [HorizontalGroup, Sirenix.OdinInspector.HideLabel] [SerializeField]
        private string id;

        /// <summary>
        /// Точка спауна (или зона спауна если есть обработка RectTransform или коллайдер TODO ? )
        /// </summary>
        [HorizontalGroup, Sirenix.OdinInspector.HideLabel] [SerializeField]
        private Transform gatePoint;

        /// <summary>
        /// ID карты с которой связана данная точка.
        /// Может быть не назначено
        /// </summary>
        [SerializeField, ValueSelector("GetTargetsList"), OnValueChanged("MakeSplit")]
        private string target;

        public string Id => id;
        public Transform GatePoint => gatePoint;

        private string _targetMapID;
        private string _targetGateID;

        /// <summary>
        /// Кеширование данных под выдачу
        /// </summary>
        private void MakeSplit()
        {
            var ar = target.Split(Divider);
            if (ar.Length != 2)
            {
                _targetMapID = String.Empty;
                _targetGateID = String.Empty;
                return;
            }

            _targetMapID = ar[0];
            _targetGateID = ar[1];
        }

        /// <summary>
        /// ID карты с которой связана данная точка.
        /// Может быть не назначено
        /// </summary>
        public string GetTargetMapID()
        {
            if (_targetMapID.IsNullOrWhitespace())
                MakeSplit();
            return _targetGateID;
        }

        public string GetTargetGateID()
        {
            if (_targetGateID.IsNullOrWhitespace())
                MakeSplit();
            return _targetMapID;
        }

#if UNITY_EDITOR
        private Dictionary<string, string> GetTargetsList()
        {
            _targetGateID = null;
            _targetMapID = null;

            var records = Database.GetRecords<MapLevelModel>();
            var list = records.OrderBy(r => r.Name);
            var result = new Dictionary<string, string> { { "[NONE]", " " } };
            foreach (var record in list)
            {
                var gates = record.GetMapGates();
                foreach (var mapGate in gates)
                    result[$"{record.Name}/{mapGate.Id}"] = $"{record.GuidPreset}{Divider}{mapGate.Id}";
            }

            return result;
        }
#endif
    }
}