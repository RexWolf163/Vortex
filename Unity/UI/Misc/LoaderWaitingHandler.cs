using System.Linq;
using UnityEngine;
using Vortex.Core.System.Abstractions;
using Vortex.Core.System.Models;
using Vortex.Unity.EditorTools.Attributes;
using Vortex.Unity.UI.TweenerSystem;

namespace Vortex.Unity.UI.Misc
{
    public class LoaderWaitingHandler : MonoBehaviour
    {
        private DelayedObserver _valve;

        [SerializeField, ClassFilter(typeof(INeedDelay))]
        private MonoBehaviour[] delayed;

        [SerializeField] private bool callOnError = true;

        [SerializeField] private TweenerHub tweener;

        private void Awake() => tweener.Back(true);

        private void OnEnable()
        {
            var ar = delayed.Select(d => d as INeedDelay).ToArray();
            _valve = new DelayedObserver(ar, OnReady, OnError, callOnError);
        }

        private void OnDisable()
        {
            _valve.Dispose();
            tweener.Back(true);
        }

        private void OnReady() => tweener.Forward();

        private void OnError(INeedDelay loader) =>
            Debug.LogError($"[LoaderWaitingHandler] loading failed for {loader.GetType().Name}.");
    }
}