using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using Vortex.Core.MappedParametersSystem.Base;

namespace Vortex.Unity.MappedParametersSystem.Base.Preset
{
    [HideReferenceObjectPicker, Serializable]
    public class MappedParameterLink : IParameterLink
    {
        [SerializeField, ValueDropdown("GetParentVariants"), HideLabel] [HorizontalGroup("Parent")]
        internal string parent;

        /// <summary>
        /// Название родительского параметра
        /// </summary>
        public string Parent => parent;

        /// <summary>
        /// Стоимость.
        /// Зто может быть стоимость развития или зависимость от значения родителя
        /// или пороговый барьер для увеличения - определяется контроллером 
        /// </summary>
        [HorizontalGroup("Parent", 50f), HideLabel] [SerializeField, Min(1)]
        internal int cost;

        /// <summary>
        /// Стоимость.
        /// Зто может быть стоимость развития или зависимость от значения родителя - определяется контроллером 
        /// </summary>
        public int Cost => cost;

#if UNITY_EDITOR
        /// <summary>
        /// Карта-владелец параметра
        /// В редакторе должна передаваться внутрь этого класса через EditorInit
        /// </summary>
        private ParametersMapStorage map;

        /// <summary>
        /// Параметр-владелец линка
        /// </summary>
        private MappedParameterPreset owner;

        public void EditorInit(ParametersMapStorage map, MappedParameterPreset owner)
        {
            this.map = map;
            this.owner = owner;
        }

        private List<string> GetParentVariants()
        {
            var list = map.GetParamsNames();
            list.Remove(owner.Name);
            return list;
        }

#endif
    }
}