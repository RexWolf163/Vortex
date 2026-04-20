using Sirenix.OdinInspector;
using UnityEngine;
using Vortex.Unity.Extensions.ReactiveValues;

namespace Vortex.Sdk.UIs.RoofTransparentSystem
{
    public class TransparentFocusHandler : MonoBehaviour
    {
        [SerializeField, InfoBox("размер объекта для расчета радиуса срабатывания прозрачности")]
        private float size;

        private readonly Vector2Data _positionContainer = new(Vector2.zero);

        private float _lastCheck;

        private const float trottling = 0.1f;

        private void Awake()
        {
            _positionContainer.SetOwner(this);
        }

        private void OnEnable()
        {
            RoofTransparentBus.Register(_positionContainer, size);
        }

        private void OnDisable()
        {
            RoofTransparentBus.Unregister(_positionContainer);
        }

        private void FixedUpdate()
        {
            if (Time.time - _lastCheck < trottling) return;
            _lastCheck = Time.time;
            _positionContainer.Set(transform.position, this);
        }
    }
}