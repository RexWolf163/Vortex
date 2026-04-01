using UnityEngine;
using UnityEngine.UI;
using Vortex.Unity.EditorTools.Attributes;

namespace Vortex.Unity.UI.Misc
{
    public class ScrollRectResetHandler : MonoBehaviour
    {
        [SerializeField, AutoLink] private ScrollRect scroll = null;

        private void Start() => scroll.normalizedPosition = Vector2.one;
    }
}