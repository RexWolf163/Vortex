using Sirenix.OdinInspector;
using Vortex.Core.LogicChainsSystem.Model;
using Vortex.Unity.EditorTools.Attributes;

namespace Vortex.Unity.LogicConditionsSystem.Conditions
{
    [ClassLabel("@ConditionName")]
    public abstract class UnityCondition : Condition
    {
        [ShowInInspector, DisplayAsString, Sirenix.OdinInspector.HideLabel, PropertyOrder(-100)]
        protected abstract string ConditionName { get; }
    }
}