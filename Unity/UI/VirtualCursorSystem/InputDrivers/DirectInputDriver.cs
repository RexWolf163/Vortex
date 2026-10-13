using System;
using UnityEngine;
using UnityEngine.InputSystem;
using Vortex.Unity.EditorTools.Attributes;
using Vortex.Unity.UI.VirtualCursorSystem.Bus;

namespace Vortex.Unity.UI.VirtualCursorSystem.InputDrivers
{
    /// <summary>
    /// Драйвер направленного ввода (стик/клавиши): каждый тик опрашивает вектор движения и сдвигает
    /// курсор. Работает на unscaledDeltaTime — действует и на паузе. Курсор не скрывает.
    ///
    /// <b>Скорость.</b> <c>speed × speedCurve(holdTime) × stick²</c>, направление — нормализованный вектор движения.
    /// <list type="bullet">
    /// <item><c>holdTime</c> — секунды удержания стика вне мёртвой зоны; в ней мгновенно сбрасывается в 0,
    /// вне её растёт до времени последнего ключа кривой и дальше не копится. Кривая задаёт профиль разгона
    /// по времени целиком: X — секунды, Y — множитель к <see cref="speed"/>. Кривая-константа — разгона нет.</item>
    /// <item><c>stick</c> — отклонение стика, перенормированное от края мёртвой зоны: 0 на её границе, 1 при полном
    /// отклонении. Возводится в квадрат — точность на малых отклонениях, полная скорость на максимальном.</item>
    /// </list>
    ///
    /// <b>Синхронизация с ОС-мышью.</b> При <see cref="warpSystemMouse"/> ОС-мышь варпается в позицию курсора
    /// на каждом тике движения — иначе событие физической мыши у <see cref="MouseInputDriver"/> вернуло бы
    /// курсор в её старую точку.
    /// </summary>
    [Serializable]
    public class DirectInputDriver : InputDriver
    {
        [SerializeField, ValueSelector("GetInputActions"),
         Tooltip("Вектор движения — Value/Vector2 (например, Gamepad/leftStick).")]
        private string moveActionId;

        [SerializeField, Tooltip("Базовая скорость курсора, пикселей/сек. Умножается на " +
                                 "speedCurve(время удержания) и на квадрат отклонения стика.")]
        private float speed = 1200f;

        [SerializeField, Range(0f, 1f), Tooltip("Мёртвая зона вектора движения.")]
        private float deadzone = 0.2f;

        [SerializeField, Tooltip("Профиль разгона: X = секунды удержания стика вне мёртвой зоны, " +
                                 "Y = множитель к speed. После последнего ключа множитель держится на его " +
                                 "значении. Дефолт — константа 1 (разгона нет). В мёртвой зоне время " +
                                 "сбрасывается — следующий старт идёт с начала кривой.")]
        private AnimationCurve speedCurve = AnimationCurve.Constant(0f, 1f, 1f);

        [SerializeField, Tooltip("Варпать ОС-мышь к виртуальному курсору на каждом тике со стиком. " +
                                 "Нужно, чтобы MouseInputDriver не откатывал позицию в старую точку " +
                                 "мыши при её дрейфе. Выключай, если ОС-курсор должен оставаться " +
                                 "независимым (например, у тебя есть отдельный ОС-курсор для чего-то).")]
        private bool warpSystemMouse = true;

        /// <summary>Экшен вектора движения (null — не разрезолвлен, драйвер бездействует).</summary>
        private InputAction _action;

        /// <summary>
        /// Секунды удержания стика вне мёртвой зоны — аргумент <see cref="speedCurve"/>.
        /// Ограничено временем последнего ключа кривой.
        /// </summary>
        private float _holdTime;

        /// <summary>Драйвер опрашивает экшен каждый кадр.</summary>
        public override bool NeedsTick => true;

        /// <summary>
        /// Резолв экшена движения (ошибка настройки — LogError, драйвер бездействует), предупреждение,
        /// если последний ключ кривой даёт 0 (курсор встанет на полном разгоне), и проверка мёртвой зоны:
        /// больше 0.5 — LogWarning, 1 и выше — LogError (стик не двигает курсор).
        /// </summary>
        public override void Connect()
        {
            _action = ResolveAction(moveActionId);

            if (string.IsNullOrEmpty(moveActionId) || _action == null)
                Debug.LogError("[DirectInputDriver] Ошибка настройки драйвера управления");

            // Кривая, оканчивающаяся на 0, — почти всегда ошибка конфигурации: после разгона
            // курсор встанет и не сдвинется, сколько стик ни держи.
            // Явно предупреждаем на подключении, не ждём «курсор не работает» в тестах.
            if (speedCurve != null && Mathf.Approximately(speedCurve.Evaluate(GetCurveEndTime()), 0f))
            {
                Debug.LogWarning(
                    $"[DirectInputDriver] speedCurve в последнем ключе ≈ 0 для moveActionId='{moveActionId}'. " +
                    "Курсор встанет после разгона — проверь форму кривой в InputDriverSet.");
            }

            // Мёртвая зона ≥ 1 глушит стик целиком (вектор длиннее 1 даёт отклонение 0), больше половины хода —
            // почти всегда ошибка настройки.
            if (deadzone >= 1f)
                Debug.LogError($"[DirectInputDriver] deadzone = {deadzone} для moveActionId='{moveActionId}': " +
                               "стик не двигает курсор — проверь мёртвую зону в InputDriverSet.");
            else if (deadzone > 0.5f)
                Debug.LogWarning($"[DirectInputDriver] deadzone = {deadzone} для moveActionId='{moveActionId}': " +
                                 "мёртвая зона больше половины хода стика — проверь настройку в InputDriverSet.");
        }

        /// <summary>Сброс экшена и времени удержания.</summary>
        public override void Disconnect()
        {
            _action = null;
            _holdTime = 0f;
        }

        /// <summary>
        /// Шаг движения: в мёртвой зоне — сброс времени удержания; иначе сдвиг курсора на
        /// <c>speed × speedCurve(holdTime) × stick² × dt</c> с клампом в экран, репорт и (опционально) варп ОС-мыши.
        /// </summary>
        public override void Tick(float unscaledDeltaTime)
        {
            if (_action == null || VirtualCursorBus.Data == null) return;

            var move = _action.ReadValue<Vector2>();
            var magnitude = move.magnitude;

            if (magnitude <= deadzone) // <=: при deadzone = 0 стик в покое не доходит до деления на нулевую длину
            {
                _holdTime = 0f; // в деад-зоне — мгновенный сброс; следующий старт пойдёт с начала кривой
                return;
            }

            var direction = move / magnitude;

            // Разгон по времени удержания: кривая целиком задаёт профиль, после последнего ключа время не копится.
            _holdTime = Mathf.Min(_holdTime + unscaledDeltaTime, GetCurveEndTime());

            // Отклонение от края мёртвой зоны до полного, в квадрате: мягко на малых, полная скорость на максимуме.
            var stick = Mathf.InverseLerp(deadzone, 1f, Mathf.Min(magnitude, 1f));

            var appliedSpeed = speed * speedCurve.Evaluate(_holdTime) * (stick * stick);
            var pos = VirtualCursorBus.Data.ScreenPosition.Value + direction * (appliedSpeed * unscaledDeltaTime);
            pos.x = Mathf.Clamp(pos.x, 0f, Screen.width - 1f);
            pos.y = Mathf.Clamp(pos.y, 0f, Screen.height - 1f);
            Report(pos, DeviceOf(_action));
            // Отклонение за мёртвой зоной — намеренный ввод: устройство репортится на каждом тике, мимо фильтра дрейфа
            // (медленный стик набирает его порог десятки кадров, а эхо варпа ОС-мыши переякоривает фильтр).
            ReportDevice(DeviceOf(_action));

            if (warpSystemMouse)
                Mouse.current?.WarpCursorPosition(pos);
        }

        /// <summary>Время последнего ключа <see cref="speedCurve"/> (0 — кривая пуста).</summary>
        private float GetCurveEndTime() =>
            speedCurve != null && speedCurve.length > 0 ? speedCurve[speedCurve.length - 1].time : 0f;
    }
}
