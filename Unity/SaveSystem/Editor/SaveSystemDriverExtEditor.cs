#if UNITY_EDITOR
using UnityEditor;
using Vortex.Core.SaveSystem.Bus;

namespace Vortex.Unity.SaveSystem
{
    public partial class SaveSystemDriver
    {
        [InitializeOnLoadMethod]
        private static void EditorRegister() => SaveController.SetDriver(Instance);
    }
}
#endif