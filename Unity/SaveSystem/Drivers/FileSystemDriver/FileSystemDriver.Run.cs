using UnityEngine;
using Vortex.Core.SaveSystem.Bus;

namespace Vortex.Unity.SaveSystem.Drivers.FileSystemDriver
{
    public sealed partial class FileSystemDriver
    {
        [RuntimeInitializeOnLoadMethod]
        private static void Run()
        {
            if (!SaveController.SetDriver(Instance))
                Dispose();
        }

        public void Init()
        {
            ScanIndex();
            OnInit?.Invoke();
        }
    }
}
