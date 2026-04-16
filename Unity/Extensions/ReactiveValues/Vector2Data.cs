using UnityEngine;
using Vortex.Core.Extensions.ReactiveValues;

namespace Vortex.Unity.Extensions.ReactiveValues
{
    public class Vector2Data : ReactiveValue<Vector2>
    {
        public Vector2Data(Vector2 value, object owner = null)
        {
            Value = value;
            _owner = owner;
        }

        public Vector2Data(Vector3 value, object owner = null)
        {
            Value = value;
            _owner = owner;
        }

        public override string ToString() => $"Vector2: {{{Value.x}, {Value.y}}}";
    }
}