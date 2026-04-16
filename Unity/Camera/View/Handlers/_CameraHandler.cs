using Sirenix.OdinInspector;
using UnityEngine;

namespace Vortex.Unity.Camera.View.Handlers
{
    /// <summary>
    /// Абстрактный хендлер для дополнения данных камеры через CameraDataStorage
    /// </summary>
    public abstract class CameraHandler : MonoBehaviour
    {
        [SerializeField] protected string cameraName;

        [InfoBox("Если ключ камеры не найден в списке - использовать любую найденную камеру")] [SerializeField]
        private bool useAnyIfNotFoundKey;

        protected CameraDataStorage Camera;

        protected virtual void OnEnable()
        {
            CameraBus.OnRegistration += OnCameraBusChanged;
            CameraBus.OnRemove += OnCameraBusChanged;
            CameraBus.TryGet(cameraName, out var camera);
            OnCameraBusChanged(camera);
        }

        protected virtual void OnDisable()
        {
            DeInit();
            CameraBus.OnRegistration -= OnCameraBusChanged;
            CameraBus.OnRemove -= OnCameraBusChanged;
        }

        private void OnCameraBusChanged(CameraDataStorage newCamera)
        {
            if (Camera != null && (newCamera == null || newCamera.gameObject.name != Camera.gameObject.name))
                return;

            if (CameraBus.TryGet(cameraName, out var camera) && camera == Camera)
                return;
            Init(camera);
        }

        private void Init(CameraDataStorage camera)
        {
            DeInit();
            Camera = camera;
            if (Camera == null && useAnyIfNotFoundKey)
                Camera = CameraBus.GetAny();

            if (Camera == null)
                return;

            SetData();
        }

        private void DeInit()
        {
            if (Camera == null)
                return;

            RemoveData();
            Camera = null;
        }


        protected abstract void SetData();
        protected abstract void RemoveData();
    }
}