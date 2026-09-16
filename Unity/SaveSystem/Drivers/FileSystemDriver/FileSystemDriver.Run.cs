using UnityEngine;
using Vortex.Core.SaveSystem.Bus;
using Vortex.Core.SettingsSystem.Bus;

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

        /// <summary>
        /// Папка сейвов берётся из настроек, поэтому индекс сканируется только после их загрузки. Порядок
        /// <c>[RuntimeInitializeOnLoadMethod]</c> между драйверами не гарантирован: в билде драйвер может
        /// подключиться раньше настроек (в редакторе они уже загружены на перезагрузке домена).
        /// </summary>
        public void Init()
        {
            Settings.OnInit -= CompleteInit;
            Settings.OnInit += CompleteInit;
        }

        private void CompleteInit()
        {
            ScanIndex();
            OnInit?.Invoke();
        }
    }
}
