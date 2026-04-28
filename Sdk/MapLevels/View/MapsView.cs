using UnityEngine;
using Vortex.Sdk.MapLevels.Bus;

namespace Vortex.Sdk.MapLevels.View
{
    public class MapsView : MonoBehaviour
    {
        private void OnEnable()
        {
            MapLevelsBus.RegisterMapsParent(transform);
        }

        private void OnDisable()
        {
            MapLevelsBus.UnregisterMapsParent(transform);
        }
    }
}