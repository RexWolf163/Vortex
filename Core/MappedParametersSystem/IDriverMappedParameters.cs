using System.Collections.Generic;
using Vortex.Core.MappedParametersSystem.Base;
using Vortex.Core.System.Abstractions;

namespace Vortex.Core.MappedParametersSystem
{
    public interface IDriverMappedParameters : ISystemDriver
    {
        /// <summary>
        /// Проброс индекса.
        /// Ключом является AssemblyQualifiedName
        /// </summary>
        /// <param name="indexMaps"></param>
        void SetIndex(Dictionary<string, ParametersMap> indexMaps);
    }
}