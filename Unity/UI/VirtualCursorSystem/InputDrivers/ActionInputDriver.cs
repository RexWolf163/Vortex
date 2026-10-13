using System;
using UnityEngine;
using Vortex.Unity.EditorTools.Attributes;
using Vortex.Unity.InputBusSystem;
using Vortex.Unity.UI.VirtualCursorSystem.Model;

namespace Vortex.Unity.UI.VirtualCursorSystem.InputDrivers
{
    /// <summary>
    /// Драйвер действий: набор привязок «экшен → <see cref="PointerAction"/>». По performed выставляет
    /// действие, по canceled снимает (одновременность поддержана). Позицию не двигает, курсор не скрывает.
    /// </summary>
    [Serializable]
    public class ActionInputDriver : InputDriver
    {
        /// <summary>Привязка «экшен-кнопка → действие курсора».</summary>
        [Serializable]
        public struct Binding
        {
            [Tooltip("Какое действие активирует привязка.")]
            public PointerAction action;

            [ValueSelector("$GetInputActions"), Tooltip("Экшен-кнопка (Button), id «Карта/Экшен».")]
            public string actionId;

#if UNITY_EDITOR
            /// <summary>
            /// Источник дропдауна id экшенов для <see cref="actionId"/>. ValueResolver резолвит имя метода
            /// относительно типа ВЛАДЕЛЬЦА поля — то есть Binding, а не ActionInputDriver; протащить сюда
            /// protected InputDriver.GetInputActions наследованием нельзя (struct не наследует класс),
            /// поэтому локальный static-прокси.
            /// </summary>
            private static string[] GetInputActions() => InputController.GetActions();
#endif
        }

        /// <summary>Привязки драйвера; на одно действие можно завести несколько экшенов.</summary>
        [SerializeField] private Binding[] bindings = Array.Empty<Binding>();

        /// <summary>
        /// Ключи удержания действий по привязкам (индекс = индекс в <see cref="bindings"/>): каждая привязка держит
        /// действие от своего имени, отпускание одной не снимает действие, удерживаемое другой.
        /// </summary>
        private object[] _sources = new object[0];

        /// <summary>
        /// Подписаться на каждую привязку: performed репортит устройство и берёт действие, canceled отпускает.
        /// Привязка с пустым или неразрезолвленным id пропускается с LogError.
        /// </summary>
        public override void Connect()
        {
            _sources = new object[bindings.Length];
            for (var i = 0; i < bindings.Length; i++)
            {
                var b = bindings[i];
                var source = _sources[i] = new object();
                var inputAction = string.IsNullOrEmpty(b.actionId) ? null : ResolveAction(b.actionId);
                if (inputAction == null)
                {
                    Debug.LogError("[ActionInputDriver] Ошибка настройки драйвера управления");
                    continue;
                }

                var action = b.action; // фиксируем на итерацию
                SubscribeAction(b.actionId,
                    () =>
                    {
                        ReportDevice(DeviceOf(inputAction));
                        Controllers.VirtualCursorController.SetAction(action, true, source);
                    },
                    () => Controllers.VirtualCursorController.SetAction(action, false, source));
            }
        }

        /// <summary>Отписаться от всех привязок и отпустить только свои удержания.</summary>
        public override void Disconnect()
        {
            for (var i = 0; i < bindings.Length; i++)
            {
                UnsubscribeAction(bindings[i].actionId);
                // Отпускаем ТОЛЬКО свои удержания, а не глобальный ClearActions: иначе отключение одного
                // драйвера стирало бы действия других источников. Неудерживаемое отпускание — no-op.
                if (i < _sources.Length)
                    Controllers.VirtualCursorController.SetAction(bindings[i].action, false, _sources[i]);
            }

            _sources = new object[0];
        }
    }
}