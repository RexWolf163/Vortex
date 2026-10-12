using System;
using UnityEngine;
using UnityEngine.InputSystem;
using Vortex.Unity.EditorTools.Attributes;

namespace Vortex.Unity.UI.VirtualCursorSystem.InputDrivers
{
    /// <summary>
    /// Драйвер мыши. Событийно репортит абсолютную позицию каждого события, без своего фильтра. Курсор не скрывает.
    /// Эхо варпа ОС-мыши (фокус-навигация, <see cref="DirectInputDriver"/>) отсекает фильтр дрейфа на входе
    /// репорта (<c>VirtualCursorController.ReportPointer</c>): оно не снимает скрытие и не выводит из режима навигации.
    /// </summary>
    [Serializable]
    public class MouseInputDriver : InputDriver
    {
        [SerializeField, ValueSelector("GetInputActions"),
         Tooltip("Позиция мыши — Value/Vector2 (например, Mouse/position).")]
        private string positionActionId;

        /// <summary>Экшен позиции мыши (null — не разрезолвлен).</summary>
        private InputAction _action;

        /// <summary>Резолв экшена позиции и подписка; ошибка настройки — LogError, драйвер бездействует.</summary>
        public override void Connect()
        {
            _action = ResolveAction(positionActionId);
            if (_action == null || string.IsNullOrEmpty(positionActionId))
            {
                Debug.LogError("[MouseInputDriver] Ошибка настройки драйвера управления");
                return;
            }

            SubscribeAction(positionActionId, OnMove, null);
        }

        /// <summary>Отписка от экшена позиции.</summary>
        public override void Disconnect()
        {
            UnsubscribeAction(positionActionId);
            _action = null;
        }

        /// <summary>Событие позиции мыши: репорт позиции.</summary>
        private void OnMove()
        {
            if (_action == null) return;
            Report(_action.ReadValue<Vector2>());
        }
    }
}