using Vortex.Core.System.Abstractions.SystemControllers;

namespace Vortex.Core.LogicChainsSystem.Model
{
    public class Connector : SystemModel
    {
        public string TargetStepGuid { get; private set; }

        public Condition[] Conditions { get; private set; }
    }
}