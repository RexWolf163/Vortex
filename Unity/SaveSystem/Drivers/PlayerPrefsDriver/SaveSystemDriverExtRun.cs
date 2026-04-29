using UnityEngine;
using Vortex.Core.SaveSystem.Bus;
using Vortex.Core.System.Abstractions;

namespace Vortex.Unity.SaveSystem.Drivers.PlayerPrefsDriver
{
    public partial class SaveSystemDriver : Singleton<SaveSystemDriver>
    {
        [RuntimeInitializeOnLoadMethod]
        private static void Run()
        {
            if (!SaveController.SetDriver(Instance))
                Dispose();
        }
    }
}
