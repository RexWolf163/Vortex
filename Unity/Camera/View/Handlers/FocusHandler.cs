using AppScripts.Camera.Controllers;
using Sirenix.OdinInspector;
using UnityEngine;

namespace AppScripts.Camera.View.Handlers
{
    public class FocusHandler : MonoBehaviour
    {
        private enum FocusMode
        {
            AddToFocus,
            NewFocus
        }

        [SerializeField] private string cameraName;

        [InfoBox("Если ключ камеры не найден в списке - использовать любую найденную камеру")] [SerializeField]
        private bool useAnyIfNotFoundKey;

        [InfoBox("Способ добавления в фокус")] [SerializeField]
        private FocusMode focusMode = FocusMode.AddToFocus;

        private CameraDataStorage _camera;

        private void OnEnable()
        {
            _camera = CameraBus.Get(cameraName);
            if (_camera == null && useAnyIfNotFoundKey)
                _camera = CameraBus.GetAny();
            if (_camera == null)
                return;

            switch (focusMode)
            {
                case FocusMode.AddToFocus:
                    _camera.AddInFocus(transform);
                    break;
                case FocusMode.NewFocus:
                    _camera.SetNewFocusGroup(transform);
                    break;
            }
        }

        private void OnDisable()
        {
            if (_camera == null)
                return;

            _camera.RemoveTargetFromFocus(transform);
            _camera = null;
        }
    }
}