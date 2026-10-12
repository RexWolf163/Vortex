using System;
using UnityEngine;
using Vortex.Unity.EditorTools.Attributes;
using Vortex.Unity.UI.VirtualCursorSystem.Focus;
using Vortex.Unity.UI.VirtualCursorSystem.Handlers;

namespace Vortex.Unity.UI.VirtualCursorSystem.InputDrivers
{
    /// <summary>
    /// Драйвер фокус-навигации: четыре action-id под направления (Up/Down/Left/Right)
    /// → на <c>performed</c> зовёт <see cref="VirtualCursorFocusController.Navigate"/>
    /// с соответствующим unit-вектором. Контроллер выбирает ближайшую цель в конусе,
    /// варпает курсор и прячет его (если включено). Action-id submit → на <c>performed</c>
    /// зовёт <see cref="VirtualCursorFocusController.Submit"/> (клик по фокусному элементу).
    ///
    /// Этот драйвер не репортит позицию и не держит <c>_action</c>-поля для polling'а —
    /// только событийная диспетчеризация в <see cref="VirtualCursorFocusController"/>.
    /// Пустой или неразрезолвленный id любого из пяти экшенов — LogError, этот экшен не подключается.
    /// Нужен <see cref="FocusTargetComponent"/> в сцене на каждом фокусируемом элементе
    /// (без них Navigate молча ничего не находит).
    /// </summary>
    [Serializable]
    public class UINavigationDriver : InputDriver
    {
        [SerializeField, ValueSelector("GetInputActions"),
         Tooltip("Направление вверх — Button (например, Gamepad/dpad/up).")]
        private string upActionId;

        [SerializeField, ValueSelector("GetInputActions"),
         Tooltip("Направление вниз — Button (например, Gamepad/dpad/down).")]
        private string downActionId;

        [SerializeField, ValueSelector("GetInputActions"),
         Tooltip("Направление влево — Button (например, Gamepad/dpad/left).")]
        private string leftActionId;

        [SerializeField, ValueSelector("GetInputActions"),
         Tooltip("Направление вправо — Button (например, Gamepad/dpad/right).")]
        private string rightActionId;

        [SerializeField, ValueSelector("GetInputActions"),
         Tooltip("активировать фокусный элемент — Button (например, Gamepad/south).")]
        private string submitActionId;

        [SerializeField, Range(1f, 89f), Tooltip("Полуугол конуса выбора (градусы). " +
                                                 "Target попадает в кандидаты, если угол " +
                                                 "к нему от направления ∈ [-cone..+cone). " +
                                                 "±45° при 4 направлениях даёт полное " +
                                                 "покрытие 360° без пересечений.")]
        private float coneAngleDeg = 45f;

        [SerializeField, Tooltip("Скрывать курсор при переходе фокуса. Last-source-wins " +
                                 "вернёт курсор видимым при движении мыши.")]
        private bool hideCursorOnFocus = true;

        [SerializeField, Tooltip("«Захват фокуса»: если в конусе направления никого нет И " +
                                 "фокус сейчас пуст (курсор свободен) — выбрать ближайший " +
                                 "активный target группы по евклидовой дистанции от курсора, " +
                                 "без учёта направления. Любая клавиша-направление становится " +
                                 "точкой входа в nav-mode. При уже активном фокусе не " +
                                 "срабатывает (чтобы навигация у края группы не прыгала " +
                                 "случайным образом).")]
        private bool captureFocusIfFree = true;

        /// <summary>Подписка на четыре направления и submit.</summary>
        public override void Connect()
        {
            SubscribeDirection(upActionId, Vector2.up);
            SubscribeDirection(downActionId, Vector2.down);
            SubscribeDirection(leftActionId, Vector2.left);
            SubscribeDirection(rightActionId, Vector2.right);
            SubscribeSubmit(submitActionId);
        }

        /// <summary>Отписка от направлений и submit.</summary>
        public override void Disconnect()
        {
            UnsubscribeAction(upActionId);
            UnsubscribeAction(downActionId);
            UnsubscribeAction(leftActionId);
            UnsubscribeAction(rightActionId);
            UnsubscribeAction(submitActionId);
        }

        /// <summary>Подписать экшен направления на <see cref="VirtualCursorFocusController.Navigate"/> с настройками драйвера.</summary>
        private void SubscribeDirection(string actionId, Vector2 direction)
        {
            if (string.IsNullOrEmpty(actionId) || ResolveAction(actionId) == null)
            {
                Debug.LogError("[UINavigationDriver] Ошибка настройки драйвера управления");
                return;
            }

            // Фиксируем направление в closure, флаги читаем из this (serializable fields
            // не меняются в рантайме после конфига).
            SubscribeAction(actionId,
                () => VirtualCursorFocusController.Navigate(direction, coneAngleDeg,
                    hideCursorOnFocus, captureFocusIfFree),
                null);
        }

        /// <summary>Подписать экшен submit на <see cref="VirtualCursorFocusController.Submit"/>.</summary>
        private void SubscribeSubmit(string actionId)
        {
            if (string.IsNullOrEmpty(actionId) || ResolveAction(actionId) == null)
            {
                Debug.LogError("[UINavigationDriver] Ошибка настройки драйвера управления");
                return;
            }

            SubscribeAction(actionId,
                VirtualCursorFocusController.Submit,
                null);
        }
    }
}