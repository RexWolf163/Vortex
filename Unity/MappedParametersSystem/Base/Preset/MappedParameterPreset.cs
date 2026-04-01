using System;
using System.Collections.Generic;
using System.Linq;
using Sirenix.OdinInspector;
using Sirenix.Utilities;
using UnityEngine;
using Vortex.Core.MappedParametersSystem.Base;
using Vortex.Unity.EditorTools.Attributes;

namespace Vortex.Unity.MappedParametersSystem.Base.Preset
{
    [HideReferenceObjectPicker, Serializable, ClassLabel("$GetFoldoutName")]
    public class MappedParameterPreset : IParameterMap
    {
        [SerializeField] internal string name;

        /// <summary>
        /// Название параметра
        /// </summary>
        public string Name => name;

        [SerializeReference, OnValueChanged("OnListChanged")]
        internal MappedParameterLink[] parents = new MappedParameterLink[0];

        /// <summary>
        /// Родители для параметра
        /// </summary>
        public IParameterLink[] Parents => parents.Select(p => p as IParameterLink).ToArray();

        /// <summary>
        /// Логика проверки стоимости (справочный параметр)
        /// </summary>
        [SerializeField, HideIf("@parents.Length <= 1")]
        internal ParameterLinkCostLogic costLogic;

        public ParameterLinkCostLogic CostLogic => costLogic;

        public int Cost { get; }

        public MappedParameterPreset()
        {
        }

        public MappedParameterPreset(string name)
        {
            this.name = name;
        }

#if UNITY_EDITOR
        /// <summary>
        /// Карта-владелец параметра
        /// В редакторе должна передаваться внутрь этого класса через EditorInit
        /// </summary>
        private ParametersMapStorage map;

        internal void EditorInit(ParametersMapStorage map)
        {
            this.map = map;
            foreach (var parent in parents)
                parent.EditorInit(map, this);
        }

        /// <summary>
        /// Сортировка списка родителей
        /// </summary>
        internal void Sort() => parents = parents.OrderBy(x => x.Parent).ToArray();

        private string GetFoldoutName()
        {
            if (Parents.IsNullOrEmpty())
                return $"{(name.IsNullOrWhitespace() ? "???" : name)} ( ??? )";
            if (Parents.Length == 1)
                return $"{(name.IsNullOrWhitespace() ? "???" : name)} ({parents[0].Parent} x {parents[0].Cost}pts.)";
            return $"{(name.IsNullOrWhitespace() ? "???" : name)} ({parents.Length} parents)";
        }

        private List<string> GetParentVariants() => map.GetParamsNames();

        private void OnListChanged() => EditorInit(map);
#endif
    }
}