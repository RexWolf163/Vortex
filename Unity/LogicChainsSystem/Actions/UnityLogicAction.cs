using Vortex.Core.LogicChainsSystem.Model;
using Vortex.Unity.EditorTools.Attributes;

namespace Vortex.Unity.LogicChainsSystem.Actions
{
    [ClassLabel("@NameAction")]
    public abstract class UnityLogicAction : LogicAction
    {
        protected abstract string NameAction { get; }
    }
}