using System;
using Sirenix.OdinInspector;
using UnityEngine;
using Vortex.Core.LogicChainsSystem.Bus;
using Vortex.Core.LogicChainsSystem.Model;
using Vortex.Unity.EditorTools.Attributes;

namespace Vortex.Unity.LogicChainsSystem.Presets
{
    [Serializable, ClassLabel("@GetConnectorName()")]
    public class ConnectorPreset
    {
        /// <summary>
        /// Цель перехода
        /// </summary>
        [SerializeReference, ValueDropdown("GetTargets"), HideReferenceObjectPicker]
        private string targetStepGuid;

        /// <summary>
        /// Перечень условий выполнения перехода
        /// </summary>
        [SerializeReference, HideReferenceObjectPicker]
        private Condition[] conditions = new Condition[0];

        /// <summary>
        /// Перечень условий выполнения перехода
        /// </summary>
        public Condition[] Conditions => conditions;

        /// <summary>
        /// Цель перехода
        /// </summary>
        public string TargetStepGuid => targetStepGuid;

#if UNITY_EDITOR
        private ChainStepPreset _owner;

        public void EditorInit(ChainStepPreset owner) => _owner = owner;

        private ValueDropdownList<string> GetTargets()
        {
            var result = new ValueDropdownList<string>();
            if (_owner == null)
                return result;
            result.Add("_CompleteChain", "-1");
            var chain = _owner.GetOwnerChain();
            var steps = chain.GetPresetSteps();
            foreach (var step in steps)
            {
                if (step.Guid == _owner.Guid)
                    continue;
                result.Add(step.Name, step.Guid);
            }

            return result;
        }

        private string GetConnectorName()
        {
            if (_owner == null)
                return "???";
            var chain = _owner.GetOwnerChain();
            var steps = chain.GetPresetSteps();
            if (targetStepGuid == LogicChains.CompleteChainStep)
                return "Complete this chain";
            foreach (var step in steps)
                if (step.Guid == targetStepGuid)
                    return $"to «{step.Name}»";

            return "Empty Connector";
        }
#endif
    }
}