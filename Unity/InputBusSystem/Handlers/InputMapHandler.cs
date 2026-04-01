using UnityEngine;
using Vortex.Core.AppSystem.Bus;
using Vortex.Core.System.Enums;
using Vortex.Unity.EditorTools.Attributes;

namespace Vortex.Unity.InputBusSystem.Handlers
{
    public class InputMapHandler : MonoBehaviour
    {
        [SerializeField, ValueSelector("GetInputMaps")]
        private string inputMap;

        private bool _wasSubscribed = false;

        private void OnEnable()
        {
            if (App.GetState() < AppStates.Running)
            {
                App.OnStateChanged += OnAppInit;
                return;
            }

            InputController.AddMapUser(inputMap, this);
            _wasSubscribed = true;
        }

        private void OnDisable()
        {
            App.OnStateChanged -= OnAppInit;
            if (!_wasSubscribed) return;
            InputController.RemoveMapUser(inputMap, this);
            _wasSubscribed = false;
        }

        private void OnDestroy() => OnDisable();

        private void OnAppInit(AppStates states)
        {
            if (states < AppStates.Running) return;
            App.OnStateChanged -= OnAppInit;
            OnEnable();
        }


#if UNITY_EDITOR
        private string[] GetInputMaps() => InputController.GetMaps();
#endif
    }
}