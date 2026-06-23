using System.Linq;
using UnityEngine;
using Vortex.Core.System.Abstractions;
using Vortex.Core.System.Models;
using Vortex.Unity.EditorTools.Attributes;
using Vortex.Unity.UI.Attributes;
using Vortex.Unity.UI.StateSwitcher;

namespace Vortex.Unity.UI.Misc
{
    public class LoaderWaitingHandler : MonoBehaviour
    {
        private enum DelayedState
        {
            Waiting,
            Ready
        }

        private DelayedObserver _valve;

        [SerializeField, ClassFilter(typeof(INeedDelay))]
        private MonoBehaviour[] delayed;

        [SerializeField] private bool callOnError = true;

        [SerializeField, StateSwitcher(typeof(DelayedState))]
        private UIStateSwitcher stateSwitcher;

        private void Awake() => stateSwitcher.ResetStates();

        private void OnEnable()
        {
            stateSwitcher.Set(DelayedState.Waiting);
            var ar = delayed.Select(d => d as INeedDelay).ToArray();
            _valve = new DelayedObserver(ar, OnReady, OnError, callOnError);
        }

        private void OnDisable() => _valve.Dispose();

        private void OnReady() => stateSwitcher.Set(DelayedState.Ready);

        private void OnError(INeedDelay loader)
        {
            Debug.LogError($"[LoaderWaitingHandler] loading failed for {loader.GetType().Name}.");
        }
    }
}