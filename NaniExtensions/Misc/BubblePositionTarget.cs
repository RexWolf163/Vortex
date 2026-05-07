using UnityEngine;

namespace Vortex.NaniExtensions.Misc
{
    public class BubblePositionTarget : MonoBehaviour
    {
        [SerializeField] private Transform target;

        public Vector3 GetPosition() => target.position;
    }
}