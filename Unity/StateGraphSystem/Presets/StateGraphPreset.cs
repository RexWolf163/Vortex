using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using Vortex.Core.DatabaseSystem.Model.Enums;
using Vortex.Core.Extensions.LogicExtensions;
using Vortex.Core.StateGraphSystem.Model;
using Vortex.Unity.DatabaseSystem.Presets;

namespace Vortex.Unity.StateGraphSystem.Presets
{
    /// <summary>
    /// Пресет автомата состояний в БД. Хранит только структуру: узлы, переходы, стартовый узел.
    /// Запись всегда MultiInstance — каждый потребитель берёт свою копию (GetNewRecord).
    /// Пустые элементы (узел/переход/условие без типа) в модель не попадают; сводка в инспекторе показывает их ошибкой
    /// </summary>
    [CreateAssetMenu(fileName = "StateGraph", menuName = "Database/State Graph")]
    public partial class StateGraphPreset : RecordPreset<StateGraph>
    {
        [SerializeField, ValueDropdown("GetNodesList"), GUIColor("GetStartColor"), FoldoutGroup("Structure", 10)]
        private string startNode;

        [SerializeReference, HideReferenceObjectPicker, FoldoutGroup("Structure")]
        private StateNodePreset[] nodes = new StateNodePreset[0];

        /// <summary>Id стартового узла</summary>
        public string StartNode => startNode;

        /// <summary>Узлы для модели: без пустых элементов</summary>
        public StateNode[] Nodes
        {
            get
            {
                var result = new List<StateNode>(nodes.Length);
                foreach (var source in nodes)
                {
                    if (source == null)
                        continue;
                    var node = new StateNode();
                    node.CopyFrom(source);
                    result.Add(node);
                }

                return result.ToArray();
            }
        }

#if UNITY_EDITOR
        /// <summary>Сырые узлы, включая пустые элементы</summary>
        internal StateNodePreset[] RawNodes => nodes;

        internal StateNodePreset FindNode(string id)
        {
            if (string.IsNullOrEmpty(id))
                return null;
            foreach (var node in nodes)
                if (node != null && node.Id == id)
                    return node;
            return null;
        }

        private void OnValidate()
        {
            type = RecordTypes.MultiInstance;
            EditorRefresh();
        }

        /// <summary>Связать узлы и переходы с пресетом (для списков редактора) и сбросить сводку</summary>
        internal void EditorRefresh()
        {
            foreach (var node in nodes)
                node?.EditorInit(this);
            ResetSummary();
        }

        private ValueDropdownList<string> GetNodesList()
        {
            var result = new ValueDropdownList<string>();
            foreach (var node in nodes)
                if (node != null)
                    result.Add(node.Label, node.Id);
            return result;
        }

        private Color GetStartColor() => FindNode(startNode) != null ? Color.white : new Color(1f, 0.3f, 0.3f, 1f);
#endif
    }
}
