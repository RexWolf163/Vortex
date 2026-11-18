using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using Vortex.Core.Extensions.LogicExtensions;
using Vortex.Core.StateGraphSystem.Model;
using Vortex.Unity.EditorTools.Attributes;

namespace Vortex.Unity.StateGraphSystem.Presets
{
    /// <summary>
    /// Узел в пресете графа. Id генерируется при создании и не меняется — на него ссылаются переходы.
    /// Пустые элементы переходов в модель не попадают (сводка пресета показывает их ошибкой)
    /// </summary>
    [Serializable, ClassLabel("@GetHeader()")]
    public class StateNodePreset
    {
        [SerializeField, HideInInspector] private string id = Crypto.GetNewGuid();

        [SerializeField, Tooltip("Уникальное имя-ключ узла: по нему View выбирает состояние свитчера")]
        private string name;

        [SerializeField, Tooltip("Реакция на Next, когда ни один переход «по Next» не прошёл по условиям")]
        private NextMissBehavior nextMiss = NextMissBehavior.Ignore;

        [SerializeReference, HideReferenceObjectPicker]
        private StateTransitionPreset[] transitions = new StateTransitionPreset[0];

        public string Id => id;

        public string Name => name;

        public NextMissBehavior NextMiss => nextMiss;

        /// <summary>Переходы для модели: без пустых элементов</summary>
        public StateTransition[] Transitions
        {
            get
            {
                var result = new List<StateTransition>(transitions.Length);
                foreach (var source in transitions)
                {
                    if (source == null)
                        continue;
                    var transition = new StateTransition();
                    transition.CopyFrom(source);
                    result.Add(transition);
                }

                return result.ToArray();
            }
        }

#if UNITY_EDITOR
        private StateGraphPreset _graph;

        internal void EditorInit(StateGraphPreset graph)
        {
            _graph = graph;
            foreach (var transition in transitions)
                transition?.EditorInit(graph);
        }

        /// <summary>Сырые переходы, включая пустые элементы</summary>
        internal StateTransitionPreset[] RawTransitions => transitions;

        internal static StateNodePreset EditorCreate(string nodeName) => new() { name = nodeName };

        internal void EditorAddTransition(string targetId)
        {
            var list = new List<StateTransitionPreset>(transitions) { StateTransitionPreset.EditorCreate(targetId) };
            transitions = list.ToArray();
        }

        internal void EditorRemoveTransition(int rawIndex)
        {
            var list = new List<StateTransitionPreset>(transitions);
            list.RemoveAt(rawIndex);
            transitions = list.ToArray();
        }

        /// <summary>Удалить все переходы на указанный узел</summary>
        internal void EditorRemoveTransitionsTo(string targetId)
        {
            var list = new List<StateTransitionPreset>(transitions);
            list.RemoveAll(transition => transition != null && transition.Target == targetId);
            transitions = list.ToArray();
        }

        /// <summary>
        /// Сырой номер перехода по номеру в модели (в модели пустые элементы отброшены); -1 — нет такого
        /// </summary>
        internal int EditorRawTransitionIndex(int modelIndex)
        {
            for (var i = 0; i < transitions.Length; i++)
            {
                if (transitions[i] == null)
                    continue;
                if (modelIndex-- == 0)
                    return i;
            }

            return -1;
        }

        /// <summary>Имя для списков редактора</summary>
        internal string Label => string.IsNullOrEmpty(name) ? "*Unnamed node*" : name;

        private string GetHeader() =>
            _graph != null && _graph.StartNode == id ? $"{Label}  [start]" : Label;
#endif
    }
}
