using UnityEngine;
using Vortex.Unity.Camera.Controllers;
using Vortex.Unity.EditorTools.Attributes;

namespace Vortex.Unity.Camera.View.Handlers
{
    [RequireComponent(typeof(RectTransform))]
    public class BordersHandler : CameraHandler
    {
        [SerializeField, AutoLink] private RectTransform rect;

        protected override void SetData()
        {
            Camera.Data.AddBorder(rect);
        }

        protected override void RemoveData()
        {
            Camera.Data?.RemoveBorder(rect);
        }
    }
}