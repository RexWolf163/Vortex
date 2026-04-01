using UnityEngine;
using Vortex.Core.DatabaseSystem.Bus;
using Vortex.Core.DatabaseSystem.Model.Enums;
using Vortex.Core.LogicChainsSystem.Bus;
using Vortex.Core.LogicChainsSystem.Model;
using Vortex.Unity.DatabaseSystem.Attributes;

namespace Vortex.Unity.LogicChainsSystem.Handlers
{
    public class LogicChainStarter : MonoBehaviour
    {
        [SerializeReference, DbRecord(typeof(LogicChain), RecordTypes.MultiInstance)]
        private string logicChain;

        private string _guid;

        private void Start()
        {
            Database.OnInit += CallChain;
        }

        private void OnDestroy()
        {
            Database.OnInit -= CallChain;
        }

        private void CallChain()
        {
            _guid = LogicChains.AddChain(logicChain);
            LogicChains.RunChain(_guid);
        }
    }
}