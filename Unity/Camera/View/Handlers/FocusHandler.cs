using Sirenix.OdinInspector;
using UnityEngine;
using Vortex.Unity.Camera.Controllers;

namespace Vortex.Unity.Camera.View.Handlers
{
    public class FocusHandler : CameraHandler
    {
        private enum FocusMode
        {
            AddToFocus,
            NewFocus
        }

        [InfoBox("Способ добавления в фокус")] [SerializeField]
        private FocusMode focusMode = FocusMode.AddToFocus;

        protected override void SetData()
        {
            switch (focusMode)
            {
                case FocusMode.AddToFocus:
                    Camera.AddInFocus(transform);
                    break;
                case FocusMode.NewFocus:
                    Camera.SetNewFocusGroup(transform);
                    break;
            }
        }

        protected override void RemoveData()
        {
            Camera.RemoveTargetFromFocus(transform);
        }
    }
}