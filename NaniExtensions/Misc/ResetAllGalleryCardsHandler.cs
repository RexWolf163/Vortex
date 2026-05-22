using Naninovel;
using UnityEngine;
using Vortex.NaniExtensions.Core;

namespace Vortex.NaniExtensions.Misc
{
    public class ResetAllGalleryCardsHandler : MonoBehaviour
    {
        public void Fire()
        {
            NaniWrapper.UnlockableManager.LockAllItems();
            NaniWrapper.StateManager.SaveGlobal().Forget();
        }
    }
}