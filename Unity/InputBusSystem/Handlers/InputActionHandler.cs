using System;
using UnityEngine;
using UnityEngine.Events;
using Vortex.Core.AppSystem.Bus;
using Vortex.Core.System.Enums;
using Vortex.Unity.EditorTools.Attributes;
using Vortex.Unity.UI.Misc;

namespace Vortex.Unity.InputBusSystem.Handlers
{
    public class InputActionHandler : MonoBehaviour
    {
        [SerializeField, ValueSelector("GetInputActions")]
        private string inputAction;

        [SerializeField] private AdvancedButton button;
        [SerializeField] private UnityEvent onPressed;
        [SerializeField] private UnityEvent onReleased;

        private bool _wasSubscribed = false;

        private void OnEnable()
        {
            if (App.GetState() < AppStates.Running)
            {
                App.OnStateChanged += OnAppInit;
                return;
            }

            InputController.AddActionUser(inputAction, this, OnPerformed, OnCanceled);
            _wasSubscribed = true;
        }

        private void OnDisable()
        {
            App.OnStateChanged -= OnAppInit;
            if (!_wasSubscribed) return;
            InputController.RemoveActionUser(inputAction, this);
            _wasSubscribed = false;
        }

        private void OnDestroy() => OnDisable();

        private void OnAppInit(AppStates states)
        {
            if (states < AppStates.Running) return;
            App.OnStateChanged -= OnAppInit;
            OnEnable();
        }

        private void OnPerformed()
        {
            button?.Press();
            onPressed?.Invoke();
        }

        private void OnCanceled()
        {
            button?.Release();
            onReleased?.Invoke();
        }


#if UNITY_EDITOR
        private string[] GetInputActions() => InputController.GetActions();
#endif
    }
}