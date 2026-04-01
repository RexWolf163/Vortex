using UnityEngine;
using UnityEngine.Events;

namespace Vortex.Unity.Components.Misc.MBHandlers
{
    public class MonoBehaviourEventsHandler : MonoBehaviour
    {
        [SerializeField] private UnityEvent onAwake = new();
        [SerializeField] private UnityEvent onDestroy = new();
        [SerializeField] private UnityEvent onEnable = new();
        [SerializeField] private UnityEvent onDisable = new();

        private void Awake()
        {
            if (enabled)
                onAwake?.Invoke();
        }

        private void OnDestroy()
        {
            if (enabled)
                onDestroy?.Invoke();
        }

        private void OnEnable()
        {
            if (isActiveAndEnabled)
                onEnable?.Invoke();
        }

        private void OnDisable()
        {
            if (isActiveAndEnabled)
                onDisable?.Invoke();
        }
    }
}