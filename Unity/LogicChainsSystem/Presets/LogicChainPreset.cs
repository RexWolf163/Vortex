using System.Collections.Generic;
using Sirenix.OdinInspector;
using Sirenix.Utilities;
using UnityEngine;
using Vortex.Core.DatabaseSystem.Model.Enums;
using Vortex.Core.Extensions.LogicExtensions;
using Vortex.Core.LogicChainsSystem.Model;
using Vortex.Unity.DatabaseSystem.Presets;

namespace Vortex.Unity.LogicChainsSystem.Presets
{
    [CreateAssetMenu(fileName = "LogicChain", menuName = "Database/Logic Chain")]
    public class LogicChainPreset : RecordPreset<LogicChain>
    {
        /// <summary>
        /// GUID начального этапа
        /// </summary>
        [SerializeField, ValueDropdown("GetStepsList"), GUIColor("TestStartStep")]
        private string startStep;

        /// <summary>
        /// Этапы цепочки
        /// </summary>
        [SerializeReference, HideReferenceObjectPicker]
        private ChainStepPreset[] chainSteps = new ChainStepPreset[0];

        /// <summary>
        /// Этапы цепочки
        /// </summary>
        public Dictionary<string, ChainStep> ChainSteps => GetSteps();

        /// <summary>
        /// GUID начального этапа
        /// </summary>
        public string StartStep => startStep;

        private Dictionary<string, ChainStep> GetSteps()
        {
            var temp = new Dictionary<string, ChainStep>();
            foreach (var chainStep in chainSteps)
            {
                var step = new ChainStep();
                ObjectExtCopy.CopyFrom(step, chainStep);
                temp.TryAdd(chainStep.Guid, step);
            }

            return temp;
        }

#if UNITY_EDITOR
        internal ChainStepPreset[] GetPresetSteps() => chainSteps;

        private void OnValidate()
        {
            type = RecordTypes.MultiInstance;
            foreach (var chainStep in chainSteps)
                chainStep.EditorInit(this);
        }

        private ValueDropdownList<string> GetStepsList()
        {
            var result = new ValueDropdownList<string>();
            foreach (var step in chainSteps)
                result.Add(StringExtensions.IsNullOrWhitespace(step.Name) ? "*Unnamed step*" : step.Name, step.Guid);
            return result;
        }

        private Color TestStartStep()
        {
            var red = new Color(1, 0.3f, 0.3f, 1);
            var white = new Color(1f, 1f, 1f, 1);
            if (StringExtensions.IsNullOrWhitespace(startStep))
                return red;
            foreach (var step in chainSteps)
            {
                if (step.Guid == startStep)
                    return white;
            }

            return red;
        }
#endif
    }
}