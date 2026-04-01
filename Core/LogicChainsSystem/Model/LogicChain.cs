using System.Collections.Generic;
using Vortex.Core.DatabaseSystem.Model;

namespace Vortex.Core.LogicChainsSystem.Model
{
    public class LogicChain : Record
    {
        /// <summary>
        /// Этапы цепочки
        /// </summary>
        public Dictionary<string, ChainStep> ChainSteps { get; protected set; }

        /// <summary>
        /// GUID начального этапа
        /// </summary>
        public string StartStep { get; protected set; }

        /// <summary>
        /// GUID текущего этапа
        /// </summary>
        public string CurrentStep { get; protected internal set; }

        public override string GetDataForSave() => CurrentStep;

        public override void LoadFromSaveData(string data) => CurrentStep = data;
    }
}