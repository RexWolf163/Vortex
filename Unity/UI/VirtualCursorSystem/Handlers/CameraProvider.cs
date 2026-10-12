using System;
using UnityEngine;
using Vortex.Unity.EditorTools.Attributes;

namespace Vortex.Unity.UI.VirtualCursorSystem.Handlers
{
    /// <summary>
    /// Провайдер геймплейной камеры для screen→world проекции. Регистрирует камеру в контроллере (LIFO —
    /// активна последняя зарегистрированная). Fail-fast, если ссылка не назначена.
    /// </summary>
    public class CameraProvider : MonoBehaviour
    {
        /// <summary>Регистрируемая камера (обязательна).</summary>
        [SerializeField, AutoLink] private Camera cam;

        [SerializeField, Tooltip("Маска слоёв для screen→world проекции.")]
        private LayerMask projectionMask = ~0;

        [SerializeField, Tooltip("Дистанция raycast проекции.")]
        private float projectionDistance = 1000f;

        /// <summary>Fail-fast: без назначенной камеры компонент бросает исключение.</summary>
        private void Awake()
        {
            if (cam == null)
                throw new InvalidOperationException($"[{name}] CameraProvider: камера не назначена.");
        }

        /// <summary>Зарегистрировать камеру поверх остальных.</summary>
        private void OnEnable() =>
            Controllers.VirtualCursorController.RegisterCamera(cam, projectionMask, projectionDistance);

        /// <summary>Снять камеру из реестра.</summary>
        private void OnDisable() => Controllers.VirtualCursorController.UnregisterCamera(cam);
    }
}