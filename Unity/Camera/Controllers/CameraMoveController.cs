using System.Collections.Generic;
using UnityEngine;
using Vortex.Unity.AppSystem.System.TimeSystem;
using Vortex.Unity.Camera.Model;
using Vortex.Unity.Camera.View;
using Object = System.Object;

namespace Vortex.Unity.Camera.Controllers
{
    public static class CameraMoveController
    {
        private static readonly Object Key = new();

        [RuntimeInitializeOnLoadMethod]
        private static void Run()
        {
            Cameras.Clear();
            CameraBus.OnRegistration -= AddListener;
            CameraBus.OnRemove -= RemoveListener;
            CameraBus.OnRegistration += AddListener;
            CameraBus.OnRemove += RemoveListener;
        }

        /// <summary>
        /// Индекс обрабатываемых камер
        /// </summary>
        private static readonly List<CameraDataStorage> Cameras = new();

        private static void AddListener(CameraDataStorage camera)
        {
            if (Cameras.Count == 0)
                TimeController.AddCallback(CalcMove);

            camera.Data.Position.SetOwner(Key);
            camera.Data.Position.Set(camera.transform.position, Key);
            camera.Data.Target.SetOwner(Key);
            camera.Data.Target.Set(camera.transform.position, Key);
            camera.Data.CallOnUpdate();

            if (!Cameras.Contains(camera))
                Cameras.Add(camera);
        }

        private static void RemoveListener(CameraDataStorage camera)
        {
            Cameras.Remove(camera);
            if (Cameras.Count == 0)
                TimeController.RemoveCallback(CalcMove);
        }

        private static void SetTarget(CameraModel cameraData)
        {
            if (cameraData.FocusedObjects == null || cameraData.FocusedObjects.Count == 0)
            {
                cameraData.Target.Set(cameraData.Position.Value, Key);
                return;
            }

            var focusGroup = cameraData.FocusedObjects[^1];
            if (focusGroup == null || focusGroup.FocusTargets.Count == 0)
                return;

            var target = Vector2.zero;
            foreach (var focus in focusGroup.FocusTargets)
                target += (Vector2)focus.position;
            target /= focusGroup.FocusTargets.Count;

            cameraData.Target.Set(target, Key);
        }

        /// <summary>
        /// Вызывается раз в кадр по FixedUpdate
        /// </summary>
        private static void CalcMove()
        {
            var c = Cameras.Count;
            for (var i = 0; i < c; i++)
            {
                if (i >= Cameras.Count)
                    return;
                var camera = Cameras[i];
                //Если есть фокус, то положение трансформа жестко задается моделью
                if (camera.Data.FocusedObjects.Count == 0)
                    camera.Data.Position.Set(camera.transform.position, Key);
                else
                {
                    var pos = camera.Data.Position.Value;
                    camera.transform.position = new Vector3(pos.x, pos.y, camera.transform.position.z);
                }

                SetTarget(camera.Data);

                camera.Data.CallOnUpdate();
            }
        }

        public static void SetPosition(this CameraModel data, Vector2 position)
        {
            data.Position.Set(position, Key);

            data.CallOnUpdate();
        }
    }
}