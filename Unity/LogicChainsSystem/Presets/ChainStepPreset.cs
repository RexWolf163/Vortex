using System;
using Sirenix.OdinInspector;
using UnityEngine;
using Vortex.Core.Extensions.LogicExtensions;
using Vortex.Core.LogicChainsSystem.Model;
using Vortex.Core.System.Abstractions.SystemControllers;
using Vortex.Unity.EditorTools.Attributes;

namespace Vortex.Unity.LogicChainsSystem.Presets
{
    [Serializable, ClassLabel("@GetStepName()")]
    public class ChainStepPreset
    {
        [SerializeField, HideInInspector] private string guid = Crypto.GetNewGuid();

        public string Guid => guid;

        [SerializeField] private string name;
        public string Name => name;

        [SerializeField] private string description;
        public string Description => description;

        [SerializeReference, HideReferenceObjectPicker]
        private LogicAction[] actions = new LogicAction[0];

        public LogicAction[] Actions => (LogicAction[])actions.Clone();

        [SerializeReference, HideReferenceObjectPicker]
        private ConnectorPreset[] connectors = new ConnectorPreset[0];

        public Connector[] Connectors
        {
            get
            {
                var list = new Connector[connectors.Length];
                for (int i = connectors.Length - 1; i >= 0; i--)
                {
                    list[i] = new Connector();
                    list[i].CopyFrom(connectors[i]);
                }

                return list;
            }
        }

#if UNITY_EDITOR
        private LogicChainPreset _owner;

        public void EditorInit(LogicChainPreset owner)
        {
            _owner = owner;
            foreach (var connector in connectors)
                connector.EditorInit(this);
        }

        public LogicChainPreset GetOwnerChain() => _owner;

        private string GetStepName() => Name.IsNullOrWhitespace() ? "Unnamed step" : Name;
#endif
    }
}