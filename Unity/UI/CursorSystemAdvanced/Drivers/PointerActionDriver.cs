using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Vortex.Unity.UI.CursorSystemAdvanced
{
    /// <summary>
    /// Драйвер действий: набор привязок «экшен → <see cref="PointerAction"/>». По started выставляет бит в маске,
    /// по canceled снимает — одновременность поддержана (несколько кнопок/скролл сразу). При потере фокуса
    /// InputSystem шлёт canceled → биты снимаются сами (alt-tab reset). Скролл-экшены (Action6/7) — обычно
    /// моментальные (тик), удержание = непрерывный скролл.
    /// </summary>
    public class PointerActionDriver : MonoBehaviour
    {
        [Serializable]
        public struct Binding
        {
            [Tooltip("Какое действие активирует эта привязка.")]
            public PointerAction action;

            [Tooltip("Экшен-кнопка (Button).")]
            public InputActionProperty input;
        }

        [SerializeField] private Binding[] bindings = Array.Empty<Binding>();

        private readonly List<(InputAction a, Action<InputAction.CallbackContext> started, Action<InputAction.CallbackContext> canceled)> _subs = new();

        private void OnEnable()
        {
            foreach (var b in bindings)
            {
                var a = b.input.action;
                if (a == null) continue;
                a.Enable();

                var action = b.action; // фиксируем на итерацию
                void Started(InputAction.CallbackContext _) => VirtualCursorController.SetAction(action, true);
                void Canceled(InputAction.CallbackContext _) => VirtualCursorController.SetAction(action, false);

                a.started += Started;
                a.canceled += Canceled;
                _subs.Add((a, Started, Canceled));
            }
        }

        private void OnDisable()
        {
            foreach (var (a, started, canceled) in _subs)
            {
                a.started -= started;
                a.canceled -= canceled;
                a.Disable();
            }

            _subs.Clear();
            VirtualCursorController.ClearActions();
        }
    }
}
