using Vortex.Core.System.Abstractions.SystemControllers;

namespace Vortex.Core.LogicChainsSystem.Model
{
    public class ChainStep : SystemModel
    {
        public string Guid { get; protected set; }
        public string Name { get; protected set; }
        public string Description { get; protected set; }
        public LogicAction[] Actions { get; protected set; }
        public Connector[] Connectors { get; protected set; }
    }
}