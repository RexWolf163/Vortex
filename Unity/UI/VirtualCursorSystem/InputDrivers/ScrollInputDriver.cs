using System;
using UnityEngine;
using UnityEngine.InputSystem;
using Vortex.Unity.EditorTools.Attributes;

namespace Vortex.Unity.UI.VirtualCursorSystem.InputDrivers
{
    /// <summary>
    /// Драйвер скролла. Четыре Button-экшена на направления (Up/Down/Left/Right) — байндятся
    /// через Rebind-пакет как любой другой Button. Поддерживает две модели ввода одновременно,
    /// чтобы «одна настройка» корректно работала и для пульсов, и для удержания:
    ///
    /// <list type="bullet">
    /// <item><b>Пульс</b> (<see cref="pulseStep"/>). На каждый <c>performed</c>-ивент шлёт один
    /// импульс <c>pulseStep</c>. Идеально для мышиного колеса
    /// (<c>&lt;Mouse&gt;/scroll/up|down|left|right</c>): каждый клик колеса даёт одно
    /// <c>performed</c> → один импульс. Быстрый скролл = несколько performed'ов за кадр →
    /// несколько импульсов → естественное ускорение.</item>
    /// <item><b>Удержание</b> (<see cref="holdSpeed"/>). В <see cref="Tick"/> опрашивает
    /// <see cref="InputAction.IsPressed"/> каждого направления и, пока зажато, шлёт
    /// <c>holdSpeed × dt</c> непрерывно. Нужно для D-Pad геймпада, клавиш клавиатуры и
    /// stick-композитов (<c>&lt;Gamepad&gt;/leftStick/up</c> как Button-биндинг): пользователь
    /// держит направление — скролл плавно едет кадр за кадром.</item>
    /// </list>
    ///
    /// Для мышиного колеса пульс и удержание не конфликтуют: performed+canceled обычно в
    /// одном кадре, <c>IsPressed()</c> в следующем Tick уже false — holdSpeed-ветка не стреляет.
    /// Для D-Pad: performed → один pulseStep (стартовый импульс), затем каждый Tick пока зажато
    /// → плавный holdSpeed × dt. Это естественное «нажал — поехал, отпустил — остановился».
    ///
    /// Выключение каждой модели — через 0 в соответствующем поле. Если <see cref="holdSpeed"/>=0
    /// — только пульсы (ступенчато, старое поведение). Если <see cref="pulseStep"/>=0 —
    /// только удержание (без стартового рывка). По умолчанию активны обе.
    ///
    /// Горизонтальные направления необязательны: оставь пустыми, если скролл только вертикальный.
    /// Курсор не скрывает, позицию не трогает. Импульсы публикуются в <c>ScrollDelta</c> модели
    /// (<c>VirtualCursorController.ReportScroll</c>); обработка — на подписчиках модели.
    /// </summary>
    [Serializable]
    public class ScrollInputDriver : InputDriver
    {
        [SerializeField, ValueSelector("GetInputActions"),
         Tooltip("Scroll↑ — Button. Пример мыши: <Mouse>/scroll/up. Пример геймпада: " +
                 "<Gamepad>/dpad/up или <Gamepad>/leftStick/up. Биндится через Rebind.")]
        private string scrollUpActionId;

        [SerializeField, ValueSelector("GetInputActions"),
         Tooltip("Scroll↓ — Button. Пример: <Mouse>/scroll/down.")]
        private string scrollDownActionId;

        [SerializeField, ValueSelector("GetInputActions"),
         Tooltip("Scroll← — Button. Пусто = горизонтальный скролл выключен.")]
        private string scrollLeftActionId;

        [SerializeField, ValueSelector("GetInputActions"),
         Tooltip("Scroll→ — Button. Пусто = горизонтальный скролл выключен.")]
        private string scrollRightActionId;

        [SerializeField, Min(0f),
         Tooltip("Импульс на один performed-ивент (пикс). Для мышиного колеса: один клик колеса → " +
                 "одно performed → один pulseStep. Для D-Pad: стартовый рывок при нажатии (до того, " +
                 "как включится hold-режим). 0 = пульсы выключены (чисто continuous от holdSpeed).")]
        private float pulseStep = 1f;

        [SerializeField, Min(0f),
         Tooltip("Скорость скролла при удержании (пикс/сек). Опрашивается в Tick каждого кадра: " +
                 "если направление зажато — шлёт holdSpeed × dt. Для D-Pad / стика / клавиш " +
                 "клавиатуры даёт плавный continuous-скролл. Мышиное колесо сюда почти не попадает " +
                 "(scroll/up фаерит один кадр и гаснет). 0 = удержание выключено (чисто ступенчатый, " +
                 "старое поведение).")]
        private float holdSpeed = 20f;

        /// <summary>Экшен направления вверх (null — не задан или не разрезолвлен).</summary>
        private InputAction _upAction;

        /// <summary>Экшен направления вниз (null — не задан или не разрезолвлен).</summary>
        private InputAction _downAction;

        /// <summary>Экшен направления влево (null — не задан или не разрезолвлен).</summary>
        private InputAction _leftAction;

        /// <summary>Экшен направления вправо (null — не задан или не разрезолвлен).</summary>
        private InputAction _rightAction;

        /// <summary>Режим удержания опрашивает экшены каждый кадр.</summary>
        public override bool NeedsTick => true;

        /// <summary>Резолв четырёх направлений и подписка на пульсы (при <see cref="pulseStep"/> &gt; 0).</summary>
        public override void Connect()
        {
            _upAction    = SubscribeDirection(scrollUpActionId,    new Vector2(0f,  1f));
            _downAction  = SubscribeDirection(scrollDownActionId,  new Vector2(0f, -1f));
            _leftAction  = SubscribeDirection(scrollLeftActionId,  new Vector2(-1f, 0f));
            _rightAction = SubscribeDirection(scrollRightActionId, new Vector2(1f,  0f));
        }

        /// <summary>Отписка от всех направлений.</summary>
        public override void Disconnect()
        {
            UnsubscribeAction(scrollUpActionId);
            UnsubscribeAction(scrollDownActionId);
            UnsubscribeAction(scrollLeftActionId);
            UnsubscribeAction(scrollRightActionId);
            _upAction = _downAction = _leftAction = _rightAction = null;
        }

        /// <summary>Режим удержания: сумма зажатых направлений × <see cref="holdSpeed"/> × dt — одним импульсом за кадр.</summary>
        public override void Tick(float unscaledDeltaTime)
        {
            if (holdSpeed <= 0f) return;

            // Накапливаем направления, зажатые в этом кадре, в один вектор — один Set в ScrollDelta
            // = одно событие в диспетчере = один raycast + scrollHandler. Если ни одно не зажато,
            // Set вообще не делаем (не тратим raycast впустую).
            var accum = Vector2.zero;
            if (_upAction    != null && _upAction.IsPressed())    accum += new Vector2(0f,  1f);
            if (_downAction  != null && _downAction.IsPressed())  accum += new Vector2(0f, -1f);
            if (_leftAction  != null && _leftAction.IsPressed())  accum += new Vector2(-1f, 0f);
            if (_rightAction != null && _rightAction.IsPressed()) accum += new Vector2(1f,  0f);

            if (accum.sqrMagnitude < 0.0001f) return;

            Controllers.VirtualCursorController.ReportScroll(accum * (holdSpeed * unscaledDeltaTime));
        }

        /// <summary>
        /// Резолв экшена направления и подписка на пульс <c>unit × pulseStep</c>.
        /// Пустой или неразрезолвленный id — направление выключено (null, без лога).
        /// </summary>
        private InputAction SubscribeDirection(string actionId, Vector2 unit)
        {
            if (string.IsNullOrEmpty(actionId)) return null;
            var action = ResolveAction(actionId);
            if (action == null) return null;
            if (pulseStep > 0f)
            {
                SubscribeAction(actionId,
                    () => Controllers.VirtualCursorController.ReportScroll(unit * pulseStep),
                    null);
            }
            return action;
        }
    }
}
