using System;
using UnityEngine;
using UnityEngine.InputSystem;
using Vortex.Core.AppSystem.Bus;
using Vortex.Core.SettingsSystem.Bus;

namespace Vortex.Unity.UI.CursorSystem
{
    /// <summary>
    /// Контроллер с состоянием курсора
    ///
    /// Логика работы - если нет дефолтной текстуры в настройках - значит курсор аппаратный
    ///
    /// Кнопки мыши слушаются через собственные InputAction (событийно, без полинга).
    /// Выделенные экшены не конфликтуют с картами InputController: action'ы не
    /// потребляют ввод эксклюзивно. При потере фокуса Input System делает
    /// soft-reset устройства и рассылает canceled — состояние кнопок
    /// синхронизируется само, залипание pressed-курсора после алт-таба исключено
    /// </summary>
    public static class CursorController
    {
        private static Vector2 _hotspot = Vector2.zero;

        private static Sprite _cursorDefault;
        private static Sprite _cursorLeftMouseDown;
        private static Sprite _cursorRightMouseDown;
        private static Sprite[] _cursorOnHover;

        private static InputAction _leftMouseAction;
        private static InputAction _rightMouseAction;
        private static readonly object Key = new();
        public static MouseKeyMap MouseKeys { get; } = new();

        [RuntimeInitializeOnLoadMethod]
        private static void Run()
        {
            Settings.OnInit -= Init;
            Settings.OnInit += Init;
            MouseKeys.LeftKeyPressed.SetOwner(Key);
            MouseKeys.RightKeyPressed.SetOwner(Key);
            MouseKeys.HoverIndex.SetOwner(Key);
        }

        private static void Init()
        {
            _cursorDefault = Settings.Data().CursorDefault;
            if (_cursorDefault == null)
                return; //Аппаратный курсор
            _cursorLeftMouseDown = Settings.Data().CursorLeftMouseDown;
            _cursorRightMouseDown = Settings.Data().CursorRightMouseDown;
            _cursorOnHover = Settings.Data().CursorOnHover;
            Apply(_cursorDefault);

            //Пересоздание при повторной инициализации (рестарт без выгрузки домена)
            DisposeActions();

            _leftMouseAction = new InputAction("CursorLeftMouse", InputActionType.Button, "<Mouse>/leftButton");
            _leftMouseAction.started += OnLeftMousePressed;
            _leftMouseAction.canceled += OnLeftMouseReleased;
            _leftMouseAction.Enable();

            _rightMouseAction = new InputAction("CursorRightMouse", InputActionType.Button, "<Mouse>/rightButton");
            _rightMouseAction.started += OnRightMousePressed;
            _rightMouseAction.canceled += OnRightMouseReleased;
            _rightMouseAction.Enable();

            App.OnExit -= DisposeActions;
            App.OnExit += DisposeActions;
        }

        private static void DisposeActions()
        {
            if (_leftMouseAction != null)
            {
                _leftMouseAction.started -= OnLeftMousePressed;
                _leftMouseAction.canceled -= OnLeftMouseReleased;
                _leftMouseAction.Dispose();
                _leftMouseAction = null;
            }

            if (_rightMouseAction != null)
            {
                _rightMouseAction.started -= OnRightMousePressed;
                _rightMouseAction.canceled -= OnRightMouseReleased;
                _rightMouseAction.Dispose();
                _rightMouseAction = null;
            }
        }

        private static void OnLeftMousePressed(InputAction.CallbackContext _)
        {
            MouseKeys.LeftKeyPressed.Set(true, Key);
            ApplyByPriority();
        }

        private static void OnLeftMouseReleased(InputAction.CallbackContext _)
        {
            MouseKeys.LeftKeyPressed.Set(false, Key);
            ApplyByPriority();
        }

        private static void OnRightMousePressed(InputAction.CallbackContext _)
        {
            MouseKeys.RightKeyPressed.Set(true, Key);
            ApplyByPriority();
        }

        private static void OnRightMouseReleased(InputAction.CallbackContext _)
        {
            MouseKeys.RightKeyPressed.Set(false, Key);
            ApplyByPriority();
        }

        public static void OnHover(int index = -1)
        {
            MouseKeys.HoverIndex.Set(index, Key);
            ApplyByPriority();
        }

        public static void OnUnHover(int index = -1)
        {
            if (MouseKeys.HoverIndex != index)
                return; //Опоздал и кто-то уже перехватил ховер в этом кадре (гонка)

            MouseKeys.HoverIndex.Set(-1, Key);
            ApplyByPriority();
        }

        private static void ApplyByPriority()
        {
            if (_cursorLeftMouseDown != null && MouseKeys.LeftKeyPressed)
            {
                Apply(_cursorLeftMouseDown);
                return;
            }

            if (_cursorRightMouseDown != null && MouseKeys.RightKeyPressed)
            {
                Apply(_cursorRightMouseDown);
                return;
            }

            if (MouseKeys.HoverIndex >= 0)
            {
                if (_cursorOnHover.Length <= MouseKeys.HoverIndex)
                    throw new IndexOutOfRangeException();

                var sprite = _cursorOnHover[MouseKeys.HoverIndex];
                if (sprite != null)
                {
                    Apply(sprite);
                    return;
                }
            }

            Apply(_cursorDefault);
        }

        /// <summary>
        /// Применение текстуры на курсор
        /// </summary>
        /// <param name="sprite"></param>
        private static void Apply(Sprite sprite)
        {
            var texture = sprite?.texture ?? _cursorDefault.texture;
            // Конвертируем нормализованный pivot в пиксельные координаты для hotspot
            _hotspot = sprite?.pivot ?? _cursorDefault.pivot;
            _hotspot.y = texture.height - _hotspot.y;
            Cursor.SetCursor(texture, _hotspot, CursorMode.Auto);
        }
    }
}