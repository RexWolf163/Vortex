#if USING_VORTEX_CURSOR
using System;
using UnityEngine;
using Vortex.Unity.EditorTools.Attributes;

namespace Vortex.Unity.UI.VirtualCursorSystem
{
    /// <summary>
    /// Драйвер действий: набор привязок «экшен → <see cref="PointerAction"/>». По performed выставляет
    /// действие, по canceled снимает (одновременность поддержана). Позицию не двигает, курсор не скрывает.
    /// </summary>
    [Serializable]
    public class ActionInputDriver : InputDriver
    {
        [Serializable]
        public struct Binding
        {
            [Tooltip("Какое действие активирует привязка.")]
            public PointerAction action;

            [ValueSelector("GetInputActions"), Tooltip("Экшен-кнопка (Button), id «Карта/Экшен».")]
            public string actionId;
        }

        [SerializeField] private Binding[] bindings = Array.Empty<Binding>();

        public override void Connect()
        {
            foreach (var b in bindings)
            {
                if (ResolveAction(b.actionId) == null) continue;
                EnableMap(b.actionId);
                var action = b.action; // фиксируем на итерацию
                SubscribeAction(b.actionId,
                    () => VirtualCursorController.SetAction(action, true),
                    () => VirtualCursorController.SetAction(action, false));
            }
        }

        public override void Disconnect()
        {
            foreach (var b in bindings)
            {
                UnsubscribeAction(b.actionId);
                DisableMap(b.actionId);
            }

            VirtualCursorController.ClearActions();
        }
    }
}
#endif
