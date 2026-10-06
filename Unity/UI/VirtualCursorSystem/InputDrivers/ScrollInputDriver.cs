using System;
using UnityEngine;
using Vortex.Unity.EditorTools.Attributes;

namespace Vortex.Unity.UI.VirtualCursorSystem
{
    /// <summary>
    /// Драйвер скролла. Четыре Button-экшена на направления (Up/Down/Left/Right), каждый на
    /// <c>performed</c> шлёт импульс <c>scrollStep</c>-пикселей в соответствующем направлении
    /// через <see cref="VirtualCursorController.ReportScroll"/>. Диспетчер подписан на
    /// <c>ScrollDelta</c> и один раз на событие выполняет raycast + <c>ExecuteEvents.scrollHandler</c>
    /// (напр. ScrollRect прокручивается на один тик).
    ///
    /// <b>Почему 4 Button, а не одно Value/Vector2.</b> Rebind-пакет работает с Button-экшенами,
    /// поэтому раскладка даёт полную совместимость: пользователь перепривязывает Scroll↑ на
    /// любой Button (Gamepad RightShoulder, Keyboard PageUp и т.д.), как любой другой экшен.
    /// Мышиное колесо в Input System раскладывается как Button-источники по направлениям
    /// (<c>&lt;Mouse&gt;/scroll/up|down|left|right</c>) — каждый тик колеса даёт одно
    /// <c>performed</c>, что идеально ложится на эту схему. Трекпад горизонтального скролла
    /// (жест двумя пальцами) OS транслирует в те же scroll/left,/right — работает автоматически.
    ///
    /// <b>Магнитуда.</b> Один тик = <see cref="scrollStep"/> пикселей. Если колесо прокручено
    /// быстро, Input System генерирует несколько performed-ивентов за кадр — драйвер шлёт
    /// несколько импульсов, ScrollRect получает их последовательно → эффективно ускоряется.
    /// Батчинга внутри кадра намеренно нет (простота + естественное ускорение от InputSystem).
    ///
    /// Горизонтальные направления (<see cref="scrollLeftActionId"/>/<see cref="scrollRightActionId"/>)
    /// необязательны: оставь пустыми, если горизонтальный скролл в проекте не нужен.
    /// Курсор не скрывает, позицию не трогает — только пушит <c>ScrollDelta</c>.
    /// </summary>
    [Serializable]
    public class ScrollInputDriver : InputDriver
    {
        [SerializeField, ValueSelector("GetInputActions"),
         Tooltip("Scroll↑ — Button. Примеры биндинга: <Mouse>/scroll/up, <Gamepad>/rightShoulder, " +
                 "<Keyboard>/pageUp. Биндится через Rebind-пакет как обычный Button-экшен.")]
        private string scrollUpActionId;

        [SerializeField, ValueSelector("GetInputActions"),
         Tooltip("Scroll↓ — Button. Пример: <Mouse>/scroll/down.")]
        private string scrollDownActionId;

        [SerializeField, ValueSelector("GetInputActions"),
         Tooltip("Scroll← — Button. Пример: <Mouse>/scroll/left. Пусто = горизонтальный скролл выключен.")]
        private string scrollLeftActionId;

        [SerializeField, ValueSelector("GetInputActions"),
         Tooltip("Scroll→ — Button. Пример: <Mouse>/scroll/right. Пусто = горизонтальный скролл выключен.")]
        private string scrollRightActionId;

        [SerializeField, Min(0.01f),
         Tooltip("Величина scrollDelta на один тик (пикс). ScrollRect по дефолту воспринимает ~1-3 на " +
                 "тик колеса. При быстром скролле Input System генерирует несколько performed-ивентов " +
                 "за кадр — эффективное ускорение берётся из их суммы.")]
        private float scrollStep = 1f;

        public override void Connect()
        {
            // X / Y — соглашение UGUI PointerEventData.scrollDelta: X — горизонталь, Y — вертикаль.
            SubscribeDirection(scrollUpActionId,    new Vector2(0f,  1f));
            SubscribeDirection(scrollDownActionId,  new Vector2(0f, -1f));
            SubscribeDirection(scrollLeftActionId,  new Vector2(-1f, 0f));
            SubscribeDirection(scrollRightActionId, new Vector2(1f,  0f));
        }

        public override void Disconnect()
        {
            UnsubscribeAction(scrollUpActionId);
            UnsubscribeAction(scrollDownActionId);
            UnsubscribeAction(scrollLeftActionId);
            UnsubscribeAction(scrollRightActionId);
            DisableMap(scrollUpActionId);
            DisableMap(scrollDownActionId);
            DisableMap(scrollLeftActionId);
            DisableMap(scrollRightActionId);
        }

        private void SubscribeDirection(string actionId, Vector2 unit)
        {
            if (string.IsNullOrEmpty(actionId)) return;
            if (ResolveAction(actionId) == null) return;
            EnableMap(actionId);
            // Closure фиксирует направление; scrollStep читаем из поля на момент тика —
            // изменение в Inspector в Play Mode применится со следующего performed.
            SubscribeAction(actionId,
                () => VirtualCursorController.ReportScroll(unit * scrollStep),
                null);
        }
    }
}
