using System;
using UnityEngine;
using Vortex.Unity.EditorTools.Attributes;

namespace Vortex.Unity.UI.CursorSystemAdvanced
{
    /// <summary>
    /// Провайдер геймплейной камеры для screen→world проекции. Регистрирует камеру в контроллере (LIFO —
    /// активна последняя зарегистрированная). Fail-fast, если ссылка не назначена.
    /// </summary>
    public class CameraProvider : MonoBehaviour
    {
        [SerializeField, AutoLink] private Camera cam;

        private void Awake()
        {
            if (cam == null)
                throw new InvalidOperationException($"[{name}] CameraProvider: камера не назначена.");
        }

        private void OnEnable() => VirtualCursorController.RegisterCamera(cam);
        private void OnDisable() => VirtualCursorController.UnregisterCamera(cam);
    }
}
