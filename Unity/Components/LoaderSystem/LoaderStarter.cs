using UnityEngine;
using Vortex.Core.AppSystem.Bus;
using Vortex.Core.LoaderSystem.Bus;
using Vortex.Core.System.Enums;

namespace Vortex.Unity.Components.LoaderSystem
{
    public class LoaderStarter : MonoBehaviour
    {
        private void OnEnable()
        {
            if (App.GetState() >= AppStates.Starting)
                Run();
            else
                App.OnStarting += Run;
        }

        private void OnDisable()
        {
            App.OnStarting -= Run;
        }

        private void Run()
        {
            Loader.Run();
        }
    }
}