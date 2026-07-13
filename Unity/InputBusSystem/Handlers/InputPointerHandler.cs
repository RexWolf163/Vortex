using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using Vortex.Core.AppSystem.Bus;
using Vortex.Core.System.Enums;
using Vortex.Unity.EditorTools.Attributes;

namespace Vortex.Unity.InputBusSystem.Handlers
{
    /// <summary>
    /// Хэндлер указателя: подписывается на экшен по правилам шины (LIFO — сигнал получает верхний
    /// подписчик) и на КАЖДОЕ его событие отдаёт наружу текущую позицию указателя одним вызовом
    /// <see cref="onPoint"/> (screen-координаты). В отличие от <see cref="InputActionHandler"/>
    /// (два события — press/release) тут один метод со значением вектора; что делать с точкой и
    /// когда начинать/заканчивать — решает подписчик.
    ///
    /// Внутри никакого гейтинга: включать/выключать ловлю — версткой (enable объекта, напр. по
    /// press-экшену через обычный <see cref="InputActionHandler"/>).
    /// </summary>
    public class InputPointerHandler : MonoBehaviour
    {
        [Serializable]
        public class PointerEvent : UnityEvent<Vector2> { }

        [SerializeField, ValueSelector("GetInputActions"),
         Tooltip("Экшен, событие которого = момент отдать позицию (напр. CursorPosition).")]
        private string inputAction;

        [SerializeField, Tooltip("Вызывается на каждое событие экшена с текущей позицией указателя (screen).")]
        private PointerEvent onPoint;

        private bool _wasSubscribed;

        private void OnEnable()
        {
            if (App.GetState() < AppStates.Running)
            {
                App.OnStateChanged += OnAppInit;
                return;
            }

            InputController.AddActionUser(inputAction, this, OnPerformed, null);
            _wasSubscribed = true;
        }

        private void OnDisable()
        {
            App.OnStateChanged -= OnAppInit;
            if (!_wasSubscribed) return;
            InputController.RemoveActionUser(inputAction, this);
            _wasSubscribed = false;
        }

        private void OnDestroy() => OnDisable();

        private void OnAppInit(AppStates states)
        {
            if (states < AppStates.Running) return;
            App.OnStateChanged -= OnAppInit;
            OnEnable();
        }

        private void OnPerformed()
        {
            if (Pointer.current != null)
                onPoint?.Invoke(Pointer.current.position.ReadValue());
        }

#if UNITY_EDITOR
        private string[] GetInputActions() => InputController.GetActions();
#endif
    }
}
