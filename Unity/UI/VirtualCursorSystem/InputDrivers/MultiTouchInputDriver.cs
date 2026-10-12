using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using Vortex.Unity.EditorTools.Attributes;
using Vortex.Unity.InputBusSystem;
using Vortex.Unity.UI.VirtualCursorSystem.Controllers;
using Vortex.Unity.UI.VirtualCursorSystem.Model;

namespace Vortex.Unity.UI.VirtualCursorSystem.InputDrivers
{
    /// <summary>
    /// Палец мультикасания по порядку прикосновения — слот <c>Touchscreen.touches[i]</c> Input System
    /// (первый приложенный палец, второй и т.д.).
    /// </summary>
    public enum TouchFinger
    {
        /// <summary>Первый палец (<c>touches[0]</c>).</summary>
        First,

        /// <summary>Второй палец (<c>touches[1]</c>).</summary>
        Second,

        /// <summary>Третий палец (<c>touches[2]</c>).</summary>
        Third,

        /// <summary>Четвёртый палец (<c>touches[3]</c>).</summary>
        Fourth,

        /// <summary>Пятый палец (<c>touches[4]</c>).</summary>
        Fifth,

        /// <summary>Шестой палец (<c>touches[5]</c>).</summary>
        Sixth,

        /// <summary>Седьмой палец (<c>touches[6]</c>).</summary>
        Seventh,

        /// <summary>Восьмой палец (<c>touches[7]</c>).</summary>
        Eighth,

        /// <summary>Девятый палец (<c>touches[8]</c>).</summary>
        Ninth,

        /// <summary>Десятый палец (<c>touches[9]</c>).</summary>
        Tenth,
    }

    /// <summary>
    /// Драйвер мультикасания: набор привязок «палец → экшен из Input Actions». Каждый кадр для привязки берётся
    /// контрол экшена, принадлежащий касанию выбранного пальца (экшен биндится на касания целиком, например
    /// <c>&lt;Touchscreen&gt;/touch*/position</c> или <c>&lt;Touchscreen&gt;/touch*/press</c>), и по его типу:
    /// <list type="bullet">
    /// <item><b>Vector2</b> (позиция) — курсор ведётся за пальцем: репорт позиции, пока касание идёт и точка
    /// сместилась (с началом касания — сразу).</item>
    /// <item><b>Кнопка</b> (press, tap) — состояние кнопки ставит или снимает <see cref="Binding.action"/> в маске
    /// <c>Actions</c>.</item>
    /// </list>
    /// Контрол другого типа или отсутствие у экшена контрола на этом пальце — LogError один раз на привязку,
    /// привязка молчит. Выключенный экшен (карта не включена) не опрашивается.
    ///
    /// Курсор скрывается по <see cref="hidesCursor"/> (прямой контакт). На платформе, где касания идут в UGUI
    /// напрямую (<c>VirtualCursorInputModule.nativeTouch</c>), курсорная часть модуля в кадрах с касанием не
    /// обрабатывается — действия этого драйвера до UGUI не дойдут; разводить по платформам <see cref="platformFilter"/>.
    /// </summary>
    [Serializable]
    public class MultiTouchInputDriver : InputDriver
    {
        /// <summary>Привязка «палец → экшен», для кнопочного контрола — действие маски.</summary>
        [Serializable]
        public struct Binding
        {
            [Tooltip("Палец по порядку прикосновения (слот Touchscreen.touches).")]
            public TouchFinger finger;

            [ValueSelector("$GetInputActions"),
             Tooltip("Экшен «Карта/Экшен», забинженный на касания (например, <Touchscreen>/touch*/position или " +
                     "<Touchscreen>/touch*/press). Берётся его контрол на выбранном пальце: Vector2 — позиция " +
                     "курсора, кнопка — действие маски.")]
            public string actionId;

            [Tooltip("Действие маски Actions для кнопочного контрола (press, tap). Для позиции не используется.")]
            public PointerAction action;

#if UNITY_EDITOR
            /// <summary>
            /// Источник дропдауна id экшенов для <see cref="actionId"/>. ValueResolver резолвит имя метода
            /// относительно типа владельца поля — Binding, поэтому локальный static-прокси (как у ActionInputDriver).
            /// </summary>
            private static string[] GetInputActions() => InputController.GetActions();
#endif
        }

        /// <summary>Рабочее состояние привязки.</summary>
        private sealed class Slot
        {
            /// <summary>Настройка привязки.</summary>
            public Binding Binding;

            /// <summary>Разрезолвленный экшен.</summary>
            public InputAction Action;

            /// <summary>Касание шло в прошлом кадре (позиция репортится с началом касания без сравнения).</summary>
            public bool WasInProgress;

            /// <summary>Последняя отрепорченная позиция.</summary>
            public Vector2 LastPosition;

            /// <summary>Кнопочный контрол был нажат в прошлом кадре.</summary>
            public bool WasPressed;

            /// <summary>Ошибка контрола уже залогирована (анти-спам).</summary>
            public bool ErrorLogged;
        }

        [SerializeField, Tooltip("Платформы подключения драйвера (см. TouchPlatformFilter).")]
        private TouchPlatformFilter platformFilter = TouchPlatformFilter.All;

        [SerializeField, Tooltip("Скрывать курсор при репорте позиции от касания (прямой контакт).")]
        private bool hidesCursor = true;

        /// <summary>Привязки пальцев.</summary>
        [SerializeField] private Binding[] bindings = new Binding[0];

        /// <summary>Подключённые привязки.</summary>
        private Slot[] _slots = new Slot[0];

        /// <summary>Привязки опрашиваются каждый кадр.</summary>
        public override bool NeedsTick => true;

        /// <summary>Скрытие курсора по <see cref="hidesCursor"/>.</summary>
        public override bool HidesCursor => hidesCursor;

        /// <summary>Поддержка платформы по <see cref="platformFilter"/>.</summary>
        public override bool SupportsPlatform(RuntimePlatform platform) => platformFilter.Matches(platform);

        /// <summary>
        /// Резолв экшенов привязок. Пустой или неразрезолвленный id — LogError, привязка пропускается.
        /// </summary>
        public override void Connect()
        {
            var slots = new List<Slot>(bindings.Length);
            foreach (var b in bindings)
            {
                var action = string.IsNullOrEmpty(b.actionId) ? null : ResolveAction(b.actionId);
                if (action == null)
                {
                    Debug.LogError("[MultiTouchInputDriver] Ошибка настройки драйвера управления");
                    continue;
                }

                slots.Add(new Slot { Binding = b, Action = action });
            }

            _slots = slots.ToArray();
        }

        /// <summary>Снять свои действия в маске и сбросить привязки.</summary>
        public override void Disconnect()
        {
            foreach (var slot in _slots)
                if (slot.WasPressed)
                    VirtualCursorController.SetAction(slot.Binding.action, false, slot);

            _slots = new Slot[0];
        }

        /// <summary>Опрос привязок: контрол экшена на пальце → позиция курсора или действие маски.</summary>
        public override void Tick(float unscaledDeltaTime)
        {
            var screen = Touchscreen.current;
            foreach (var slot in _slots)
            {
                if (!slot.Action.enabled)
                {
                    // Карта выключена — касание для драйвера закончилось: отпускаем действие, сбрасываем ведение.
                    TickButton(slot, false);
                    slot.WasInProgress = false;
                    continue;
                }

                var index = (int)slot.Binding.finger;
                var touch = screen != null && index < screen.touches.Count ? screen.touches[index] : null;
                var control = touch != null ? FindControl(slot.Action, touch) : null;

                switch (control)
                {
                    case ButtonControl button:
                        TickButton(slot, button.isPressed);
                        break;

                    case InputControl<Vector2> position:
                        TickPosition(slot, touch.isInProgress, position.ReadValue());
                        break;

                    case null when screen == null:
                        // Тач-экрана нет (ещё не подключён) — привязке нечего читать.
                        TickButton(slot, false);
                        break;

                    default:
                        TickButton(slot, false);
                        if (!slot.ErrorLogged)
                        {
                            Debug.LogError($"[MultiTouchInputDriver] У экшена '{slot.Binding.actionId}' нет контрола " +
                                           $"позиции или кнопки на пальце {slot.Binding.finger}. Забиндь его на " +
                                           "<Touchscreen>/touch*/position или <Touchscreen>/touch*/press.");
                            slot.ErrorLogged = true;
                        }

                        break;
                }
            }
        }

        /// <summary>Кнопочный контрол: смена состояния ставит или снимает действие маски.</summary>
        private static void TickButton(Slot slot, bool pressed)
        {
            if (pressed == slot.WasPressed) return;
            slot.WasPressed = pressed;
            VirtualCursorController.SetAction(slot.Binding.action, pressed, slot);
        }

        /// <summary>Позиционный контрол: репорт, пока касание идёт и точка сместилась (с началом касания — сразу).</summary>
        private void TickPosition(Slot slot, bool inProgress, Vector2 pos)
        {
            if (!inProgress)
            {
                slot.WasInProgress = false;
                return;
            }

            if (slot.WasInProgress && pos == slot.LastPosition) return;

            slot.WasInProgress = true;
            slot.LastPosition = pos;
            Report(pos);
        }

        /// <summary>Контрол экшена, принадлежащий касанию пальца (<paramref name="touch"/> или его потомок); null — нет.</summary>
        private static InputControl FindControl(InputAction action, TouchControl touch)
        {
            foreach (var control in action.controls)
                for (var c = control; c != null; c = c.parent)
                    if (c == touch)
                        return control;

            return null;
        }
    }
}
