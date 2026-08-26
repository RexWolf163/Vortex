using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.UI;

namespace Vortex.Unity.UI.CursorSystem
{
    /// <summary>
    /// Гейт одновременной работы мыши и геймпада для курсора <see cref="CursorController"/>.
    ///
    /// <see cref="VirtualMouseInput"/> предполагается в режиме Hardware Cursor: он варпит СИСТЕМНУЮ мышь от стика,
    /// своего графического курсора не рисует — визуал целиком за <see cref="CursorController"/> (<c>Cursor.SetCursor</c>
    /// ForceSoftware по позиции ОС-курсора). Проблема режима: VMI варпит системную мышь каждый кадр и «перехватывает»
    /// реальную мышь.
    ///
    /// Гейт держит VMI ВЫКЛЮЧЕННЫМ (мышь работает нативно, <see cref="CursorController"/> рисует по её позиции) и
    /// включает его ТОЛЬКО пока игрок реально двигает стик / жмёт геймпад-кнопки. Приоритет у геймпада лишь пока стик
    /// активен — поэтому возможная варп-дельта не сбивает режим; отпустил стик → мышь снова главная по первому же
    /// реальному движению. При входе в геймпад-режим позиция виртуальной мыши синхронизируется с реальной —
    /// курсор не прыгает.
    ///
    /// Кнопки в геймпад-режиме даёт сама виртуальная мышь VMI (<c>&lt;Mouse&gt;/leftButton|rightButton</c>) —
    /// их читают и <see cref="CursorController"/> (Action/AltAction-спрайт), и UI-модуль (клик). Инъекции не нужно.
    /// </summary>
    [RequireComponent(typeof(VirtualMouseInput))]
    public class PointerModeGate : MonoBehaviour
    {
        [SerializeField, Tooltip("Виртуальная мышь. Пусто — берётся с этого же объекта.")]
        private VirtualMouseInput virtualMouseInput;

        [SerializeField, Range(0f, 1f), Tooltip("Мёртвая зона стика: ниже — геймпад считается неактивным.")]
        private float stickDeadzone = 0.2f;

        [SerializeField, Tooltip("Порог смещения реальной мыши (пиксели/кадр), выше которого мышь перехватывает управление.")]
        private float mouseMoveThreshold = 1f;

        private bool _gamepadMode;
        private Mouse _realMouse;

        private void Awake()
        {
            if (virtualMouseInput == null)
                virtualMouseInput = GetComponent<VirtualMouseInput>();

            // Стартуем в режиме мыши: гасим VMI до его OnEnable (Awake раньше фазы OnEnable) — виртуальная мышь не создаётся,
            // системная мышь работает нативно, CursorController рисует по ней.
            SetGamepadMode(false, force: true);
        }

        private void OnDisable()
        {
            if (virtualMouseInput != null)
                virtualMouseInput.enabled = false;
            _gamepadMode = false;
        }

        private void Update()
        {
            var pad = Gamepad.current;
            var gamepadActive = pad != null && (
                pad.leftStick.ReadValue().sqrMagnitude > stickDeadzone * stickDeadzone ||
                pad.buttonSouth.isPressed || pad.buttonEast.isPressed);

            if (gamepadActive)
            {
                SetGamepadMode(true);
                return; // пока стик активен — мышь не проверяем: варп-дельта не собьёт режим
            }

            var real = ResolveRealMouse();
            var mouseActive = real != null && (
                real.delta.ReadValue().sqrMagnitude > mouseMoveThreshold * mouseMoveThreshold ||
                real.leftButton.isPressed || real.rightButton.isPressed || real.middleButton.isPressed);

            if (mouseActive)
                SetGamepadMode(false);
        }

        private void SetGamepadMode(bool on, bool force = false)
        {
            if (!force && on == _gamepadMode)
                return;
            _gamepadMode = on;

            if (virtualMouseInput == null)
                return;

            if (on)
            {
                virtualMouseInput.enabled = true; // OnEnable создаёт виртуальную мышь

                // Синк: стартуем виртуальный курсор оттуда, где сейчас реальный — без прыжка.
                // VMI каждый кадр читает позицию своего устройства и прибавляет дельту стика, так что подхватит.
                var real = ResolveRealMouse();
                var vm = virtualMouseInput.virtualMouse;
                if (real != null && vm != null)
                    InputState.Change(vm.position, real.position.ReadValue());
            }
            else
            {
                // OnDisable убирает виртуальную мышь; системная мышь осталась там, куда её отварпил VMI — прыжка нет.
                virtualMouseInput.enabled = false;
            }
        }

        // Реальная (физическая) мышь — не виртуальная от VMI. Нужна, т.к. Mouse.current в геймпад-режиме = виртуальная.
        private Mouse ResolveRealMouse()
        {
            var vm = virtualMouseInput != null ? virtualMouseInput.virtualMouse : null;
            if (_realMouse != null && _realMouse.added && _realMouse != vm)
                return _realMouse;

            foreach (var device in InputSystem.devices)
            {
                if (device is Mouse m && m != vm)
                {
                    _realMouse = m;
                    return m;
                }
            }

            return null;
        }
    }
}
