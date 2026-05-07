using UnityEngine;
using Vortex.Unity.EditorTools.Attributes;
using Vortex.Unity.UI.TweenerSystem;

namespace Vortex.NaniExtensions.Misc
{
    public class VisibilityCharacterHandler : MonoBehaviour
    {
        [SerializeField, AutoLink] private TweenerHub tweener;

        private void OnEnable()
        {
            tweener.Back(true);
        }

        public void SetVisibility(bool b)
        {
            if (b)
                tweener.Forward();
            else
                tweener.Back();
        }
    }
}