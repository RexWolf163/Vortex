using System;
using System.Linq;
using Sirenix.OdinInspector;
using UnityEngine;
using Vortex.Core.LogicChainsSystem.Model;
using Vortex.Unity.EditorTools.Attributes;

namespace Vortex.Unity.StateGraphSystem.Presets
{
    /// <summary>
    /// Переход узла в пресете графа. Цель — Id узла. Пустые элементы условий в модель не попадают
    /// (сводка пресета показывает их ошибкой)
    /// </summary>
    [Serializable, ClassLabel("@GetLabel()")]
    public class StateTransitionPreset
    {
        [SerializeField, ValueDropdown("GetTargets"), GUIColor("GetTargetColor")]
        private string target;

        [SerializeField, Tooltip("Переход ждёт сигнала Next. Выключено — автопереход по условиям")]
        private bool waitNext;

        [SerializeReference, HideReferenceObjectPicker]
        private Condition[] conditions = new Condition[0];

        /// <summary>Id узла-цели</summary>
        public string Target => target;

        /// <summary>Переход ждёт сигнала Next</summary>
        public bool WaitNext => waitNext;

        /// <summary>Условия перехода без пустых элементов</summary>
        public Condition[] Conditions => conditions.Where(condition => condition != null).ToArray();

#if UNITY_EDITOR
        private StateGraphPreset _graph;

        internal void EditorInit(StateGraphPreset graph) => _graph = graph;

        internal static StateTransitionPreset EditorCreate(string targetId) => new() { target = targetId };

        internal void EditorSetWaitNext(bool value) => waitNext = value;

        /// <summary>Сырые условия, включая пустые элементы</summary>
        internal Condition[] RawConditions => conditions;

        private ValueDropdownList<string> GetTargets()
        {
            var result = new ValueDropdownList<string>();
            if (_graph == null)
                return result;
            foreach (var node in _graph.RawNodes)
                if (node != null)
                    result.Add(node.Label, node.Id);
            return result;
        }

        private Color GetTargetColor() =>
            _graph != null && _graph.FindNode(target) != null ? Color.white : new Color(1f, 0.3f, 0.3f, 1f);

        private string GetLabel()
        {
            var node = _graph?.FindNode(target);
            var name = node == null ? "???" : $"«{node.Label}»";
            return $"→ {name}{(waitNext ? " (Next)" : "")}";
        }
#endif
    }
}
