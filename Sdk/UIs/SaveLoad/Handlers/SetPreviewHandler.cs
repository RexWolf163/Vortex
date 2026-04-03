using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;

namespace Vortex.Sdk.UIs.SaveLoad.Handlers
{
    public class SetPreviewHandler : MonoBehaviour

    {
        [SerializeField] private RawImage image;

        [Button]
        private void SnapShot()
        {
            image.texture = CameraCaptureHandler.Capture();
        }

        [Button]
        private void Transition()
        {
            SavePreviewController.SavePreview((Texture2D)image.texture, "test");
            image.texture = SavePreviewController.GetPreview("test");
        }
    }
}