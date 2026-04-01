using System;
using System.Collections.Generic;
using Vortex.Core.Extensions.LogicExtensions.Actions;
using Vortex.Core.MappedParametersSystem;
using Vortex.Core.MappedParametersSystem.Base;
using Vortex.Core.System.Abstractions;
using Vortex.Unity.MappedParametersSystem.Base.Preset;

namespace Vortex.Unity.MappedParametersSystem
{
    public partial class MappedParametersDriver : Singleton<MappedParametersDriver>, IDriverMappedParameters
    {
        public event Action OnInit;

        private Dictionary<string, ParametersMap> _indexMaps;

        public void Init()
        {
            OnInit.Fire();
        }

        public void Destroy()
        {
        }

        public void SetIndex(Dictionary<string, ParametersMap> indexMaps)
        {
            _indexMaps = indexMaps;
        }
    }
}