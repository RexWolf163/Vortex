using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Vortex.Sdk.RebindSystem.Presets
{
    /// <summary>
    /// Пара команд одной карты, которым разрешено делить клавишу. Их пересечение показывается состоянием
    /// «разрешённый конфликт» и не блокирует назначение.
    /// </summary>
    [Serializable, HideReferenceObjectPicker]
    public class CommandPair
    {
        [SerializeField, HorizontalGroup, HideLabel, ValueDropdown("CommandIds")]
        private string first;

        [SerializeField, HorizontalGroup, HideLabel, ValueDropdown("CommandIds")]
        private string second;

        public string First => first;

        public string Second => second;

        /// <summary>Пара описывает эти две команды, в любом порядке.</summary>
        public bool Matches(string a, string b) =>
            (first == a && second == b) || (first == b && second == a);

#if UNITY_EDITOR
        private IEnumerable<string> CommandIds() => RebindEditorLists.CommandIds();
#endif
    }
}
