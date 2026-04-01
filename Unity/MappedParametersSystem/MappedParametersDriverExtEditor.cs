using UnityEditor;
using UnityEngine;
using Vortex.Core.Extensions.LogicExtensions;
using Vortex.Core.MappedParametersSystem.Bus;
using Vortex.Unity.FileSystem.Bus;
using Vortex.Unity.MappedParametersSystem.Base.Preset;

#if UNITY_EDITOR
namespace Vortex.Unity.MappedParametersSystem
{
    public partial class MappedParametersDriver
    {
        private const string Path = "MapsConfig";

        [InitializeOnLoadMethod]
        private static void EditorRegister()
        {
            if (!ParameterMaps.SetDriver(Instance))
                return;

            File.CreateFolders($"{Application.dataPath}/Resources/{Path}");
            Instance.LoadData();
        }

        private void LoadData()
        {
            var resources = Resources.LoadAll<ParametersMapStorage>("");
            if (resources == null || resources.Length == 0)
            {
                Debug.LogWarning("MappedParameters assets not found. Switch off this Driver if not use this system.");
                return;
            }

            _indexMaps.Clear();
            foreach (var map in resources)
                _indexMaps.AddNew(map.guid, GetMap(map));
        }
    }
}
#endif