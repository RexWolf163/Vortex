using Sirenix.OdinInspector;
using UnityEngine;

namespace Vortex.Sdk.UIs.RoofTransparentSystem
{
    public class TriggerZone : MonoBehaviour
    {
        /// <summary>
        /// Граница срабатывания триггера
        /// </summary>
        [SerializeField, InfoBox("Граница срабатывания триггера")] [Range(0.01f, 2f)]
        private float radius = 0.1f;

        public Vector2 Position => transform.position;

        public float Radius => radius;

#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, Radius);
        }
#endif
    }
}