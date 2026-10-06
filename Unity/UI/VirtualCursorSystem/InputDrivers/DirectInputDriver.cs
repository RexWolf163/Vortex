using System;
using UnityEngine;
using UnityEngine.InputSystem;
using Vortex.Unity.EditorTools.Attributes;

namespace Vortex.Unity.UI.VirtualCursorSystem
{
    /// <summary>
    /// Драйвер направленного ввода (источник <see cref="PointerSourceKind.Direct"/>): интегрирует вектор
    /// движения (стик/клавиши) в позицию курсора в <see cref="Tick"/>. Работает на unscaledDeltaTime —
    /// действует и на паузе (меню). Курсор не скрывает.
    ///
    /// <b>Профиль скорости.</b> Итоговая скорость =
    /// <c>speed × <see cref="speedCurve"/>.Evaluate(stickMagnitude) × accelFactor</c>, где:
    /// (1) <see cref="speedCurve"/> — нелинейный ответ на отклонение стика
    /// (вход X = <c>stickMagnitude ∈ [0..1]</c>, выход Y = множитель к <see cref="speed"/>).
    /// Дефолт — константа 1 (кривая не влияет; задаётся дизайнером при необходимости — например,
    /// Pow(x, 2) для более точного управления на малых отклонениях).
    /// (2) <see cref="accelerationTime"/> — время плавного набора скорости от 0 до максимума
    /// (нет мгновенного рывка на старте). При входе в deadzone фактор мгновенно сбрасывается
    /// в 0 — следующий старт тоже плавный. Торможение в деад-зоне мгновенное: курсор нужно
    /// отпускать быстро, не инерцией. Для 0 — классический bang-bang (как было без разгона).
    ///
    /// <b>Синхронизация с ОС-мышью.</b> При движении стиком дополнительно вызывается
    /// <see cref="Mouse.WarpCursorPosition"/>: без этого реальная мышь остаётся в своей
    /// старой позиции, и любой её дрейф/касание рождает <c>performed</c>-event, который
    /// у <see cref="MouseInputDriver"/> безусловно перепишет позицию виртуального
    /// курсора — визуально это выглядит как «курсор прыгает к старой позиции мыши».
    /// Варп держит ОС-мышь синхронно с виртуальной позицией; event мыши при дрейфе
    /// придёт с той же точкой, и перехода не будет. Опционально отключается
    /// <see cref="warpSystemMouse"/>, если ОС-курсор нужен в своём независимом месте.
    /// </summary>
    [Serializable]
    public class DirectInputDriver : InputDriver
    {
        [SerializeField, ValueSelector("GetInputActions"),
         Tooltip("Вектор движения — Value/Vector2 (например, Gamepad/leftStick).")]
        private string moveActionId;

        [SerializeField, Tooltip("Максимальная скорость курсора, пикселей/сек. Умножается на " +
                                 "speedCurve(stickMagnitude) и на accelFactor (плавный разгон).")]
        private float speed = 1200f;

        [SerializeField, Range(0f, 1f), Tooltip("Мёртвая зона вектора движения.")]
        private float deadzone = 0.2f;

        [SerializeField, Tooltip("Форма ответа на отклонение стика: X = магнитуда [0..1], " +
                                 "Y = множитель к speed. Дефолт — константа 1 (кривая не влияет). " +
                                 "Для гейпадов часто используется Pow(x, 2) для более плавного " +
                                 "управления на малых отклонениях.")]
        private AnimationCurve speedCurve = AnimationCurve.Constant(0f, 1f, 1f);

        [SerializeField, Min(0f), Tooltip("Время плавного набора скорости (секунды) с момента " +
                                          "выхода стика из деад-зоны. 0 = мгновенный разгон (bang-bang). " +
                                          "В деад-зоне фактор мгновенно сбрасывается — следующий старт " +
                                          "начинается с 0.")]
        private float accelerationTime = 0.1f;

        [SerializeField, Tooltip("Варпать ОС-мышь к виртуальному курсору на каждом тике со стиком. " +
                                 "Нужно, чтобы MouseInputDriver не откатывал позицию в старую точку " +
                                 "мыши при её дрейфе. Выключай, если ОС-курсор должен оставаться " +
                                 "независимым (например, у тебя есть отдельный ОС-курсор для чего-то).")]
        private bool warpSystemMouse = true;

        private InputAction _action;
        private float _accelFactor;

        public override bool NeedsTick => true;

        public override void Connect()
        {
            _action = ResolveAction(moveActionId);
            EnableMap(moveActionId);

            // Curve, оканчивающаяся на 0 при полном отклонении стика (Evaluate(1) ≈ 0) —
            // почти всегда ошибка конфигурации: курсор не будет двигаться даже на максимуме.
            // Явно предупреждаем на подключении, не ждём «курсор не работает» в тестах.
            if (speedCurve != null && Mathf.Approximately(speedCurve.Evaluate(1f), 0f))
            {
                Debug.LogWarning(
                    $"[DirectInputDriver] speedCurve.Evaluate(1) ≈ 0 для moveActionId='{moveActionId}'. " +
                    "Курсор не будет двигаться при полном отклонении стика — проверь форму кривой в InputDriverSet.");
            }
        }

        public override void Disconnect()
        {
            DisableMap(moveActionId);
            _action = null;
            _accelFactor = 0f;
        }

        public override void Tick(float unscaledDeltaTime)
        {
            if (_action == null || VirtualCursorBus.Data == null) return;

            var move = _action.ReadValue<Vector2>();
            var magnitude = move.magnitude;

            if (magnitude < deadzone)
            {
                _accelFactor = 0f; // в деад-зоне — мгновенный сброс; следующий старт пойдёт с 0
                return;
            }

            // Нормировка магнитуды к [0..1] для curve: ось X интерпретируется как доля
            // отклонения стика. При magnitude > 1 (бывает у некоторых композитных биндингов —
            // например, keyboard WASD без normalize) кривая выходит за правый край,
            // AnimationCurve.Evaluate штатно возвращает экстраполированное значение.
            var curveMultiplier = speedCurve.Evaluate(Mathf.Clamp01(magnitude));
            var direction = move / magnitude;

            // Плавный разгон: за accelerationTime секунд выходим с 0 на 1.
            // Нулевое время — мгновенный выход (bang-bang как было).
            if (accelerationTime > 0f)
                _accelFactor = Mathf.MoveTowards(_accelFactor, 1f, unscaledDeltaTime / accelerationTime);
            else
                _accelFactor = 1f;

            var appliedSpeed = speed * curveMultiplier * _accelFactor;
            var pos = VirtualCursorBus.Data.ScreenPosition.Value + direction * (appliedSpeed * unscaledDeltaTime);
            pos.x = Mathf.Clamp(pos.x, 0f, Screen.width - 1f);
            pos.y = Mathf.Clamp(pos.y, 0f, Screen.height - 1f);
            Report(pos, PointerSourceKind.Direct);

            if (warpSystemMouse)
                Mouse.current?.WarpCursorPosition(pos);
        }
    }
}
