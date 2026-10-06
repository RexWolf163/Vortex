using System;
using UnityEngine;
using Vortex.Unity.EditorTools.Attributes;
using Vortex.Unity.InputBusSystem;

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

            [ValueSelector(nameof(GetInputActions)), Tooltip("Экшен-кнопка (Button), id «Карта/Экшен».")]
            public string actionId;

#if UNITY_EDITOR
            // ValueResolver резолвит имя метода относительно типа ВЛАДЕЛЬЦА поля — то есть
            // Binding, а не ActionInputDriver. Протащить сюда protected InputDriver.GetInputActions
            // наследованием нельзя (struct не наследует класс). Локальный static-прокси —
            // минимальный фикс, иерархию обходить вручную через @root/@# не надо.
            private static string[] GetInputActions() => InputController.GetActions();
#endif
        }

        [SerializeField] private Binding[] bindings = Array.Empty<Binding>();

        public override void Connect()
        {
            foreach (var b in bindings)
            {
                if (ResolveAction(b.actionId) == null)
                {
                    if (!string.IsNullOrEmpty(b.actionId))
                        Debug.LogError($"[ActionInputDriver] Экшен '{b.actionId}' не разрезолвился — " +
                                       $"привязка '{b.action}' работать не будет. Проверь id в InputDriverSet.");
                    continue;
                }
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
                // Снимаем ТОЛЬКО свои биты, а не глобальный ClearActions: иначе отключение одного
                // ActionInputDriver стирало бы маску действий, выставленную другими экземплярами.
                VirtualCursorController.SetAction(b.action, false);
            }
        }
    }
}
