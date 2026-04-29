#if UNITY_EDITOR
using UnityEditor;
using Vortex.Core.SaveSystem.Bus;

namespace Vortex.Unity.SaveSystem.Drivers.FileSystemDriver
{
    public sealed partial class FileSystemDriver
    {
        [InitializeOnLoadMethod]
        private static void EditorRegister() => SaveController.SetDriver(Instance);
    }
}
#endif
