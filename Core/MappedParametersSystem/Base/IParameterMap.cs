namespace Vortex.Core.MappedParametersSystem.Base
{
    public interface IParameterMap
    {
        public string Name { get; }
        public IParameterLink[] Parents { get; }
        public int Cost { get; }
        public ParameterLinkCostLogic CostLogic { get; }
    }
}