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
    /// Логика работы - если в настройках нет наборов курсоров - значит курсор аппаратный.
    /// Набор выбирается по текущему Screen.height (см. <see cref="SelectPack"/>);
    /// после смены разрешения перевыбор делается через <see cref="RefreshResolution"/>
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

        /// <summary>
        /// Публичный наблюдаемый снимок текущего состояния мыши: нажатие LMB/RMB
        /// и активный hover-индекс. Поля — <see cref="Vortex.Core.Extensions.ReactiveValues.BoolData"/>
        /// и <see cref="Vortex.Core.Extensions.ReactiveValues.IntData"/>, можно подписаться
        /// на <c>OnUpdate</c> для реакции на изменения снаружи.
        ///
        /// Запись в эти поля защищена <c>SetOwner(Key)</c> — снаружи изменить значения
        /// невозможно, мутацию делает только сам <see cref="CursorController"/>.
        /// </summary>
        public static MouseKeyMap MouseKeys { get; } = new();

        /// <summary>
        /// Авто-инициализация при старте рантайма: подписка на <see cref="Settings.OnInit"/>
        /// (контроллер дёрнется, когда настройки прочитаны) и закрепление owner'а за полями
        /// <see cref="MouseKeys"/>, чтобы их нельзя было перезаписать снаружи.
        /// </summary>
        [RuntimeInitializeOnLoadMethod]
        private static void Run()
        {
            Settings.OnInit -= Init;
            Settings.OnInit += Init;
            MouseKeys.LeftKeyPressed.SetOwner(Key);
            MouseKeys.RightKeyPressed.SetOwner(Key);
            MouseKeys.HoverIndex.SetOwner(Key);
        }

        /// <summary>
        /// Читает текущие настройки курсора, выбирает набор под разрешение и поднимает
        /// подписки на InputSystem. Если наборы не заданы — считаем что пользователь
        /// хочет аппаратный курсор, контроллер ничего не делает и подписки не создаёт.
        ///
        /// Повторный вызов (рестарт без выгрузки домена) сначала отвязывает старые
        /// <see cref="InputAction"/> через <see cref="DisposeActions"/> — двойной подписки
        /// не происходит.
        /// </summary>
        private static void Init()
        {
            var packs = Settings.Data().CursorPacks;
            if (packs == null || packs.Length == 0)
                return; //Аппаратный курсор

            SelectPack(packs);
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

        /// <summary>
        /// Отвязывает коллбэки от LMB/RMB <see cref="InputAction"/> и освобождает их.
        /// Зовётся в начале <see cref="Init"/> (для защиты от двойной подписки при рестарте)
        /// и из <see cref="App.OnExit"/> (для штатной очистки ресурсов InputSystem).
        /// </summary>
        /// <summary>
        /// Выбор набора курсоров под текущее разрешение.
        /// Берётся набор с минимальным MaxScreenHeight >= Screen.height;
        /// если разрешение выше всех порогов — самый крупный набор
        /// </summary>
        private static void SelectPack(CursorResolutionPack[] packs)
        {
            var height = Screen.height;
            CursorPack selected = null;
            CursorPack largest = null;
            var bestMax = int.MaxValue;
            var largestMax = int.MinValue;

            foreach (var entry in packs)
            {
                if (entry?.Pack == null)
                    continue;

                if (entry.MaxScreenHeight >= height && entry.MaxScreenHeight < bestMax)
                {
                    bestMax = entry.MaxScreenHeight;
                    selected = entry.Pack;
                }

                if (entry.MaxScreenHeight > largestMax)
                {
                    largestMax = entry.MaxScreenHeight;
                    largest = entry.Pack;
                }
            }

            selected ??= largest;

            _cursorDefault = selected.CursorDefault;
            _cursorLeftMouseDown = selected.CursorLeftMouseDown;
            _cursorRightMouseDown = selected.CursorRightMouseDown;
            _cursorOnHover = selected.CursorOnHover;
        }

        /// <summary>
        /// Перевыбор набора курсоров под текущее разрешение.
        /// Дёргать после применения видеонастроек (смена разрешения/режима окна).
        /// Состояние кнопок и ховера сохраняется — применяется через приоритеты
        /// </summary>
        public static void RefreshResolution()
        {
            var packs = Settings.Data().CursorPacks;
            if (packs == null || packs.Length == 0 || _cursorDefault == null)
                return; //Аппаратный курсор или контроллер не инициализирован

            SelectPack(packs);
            ApplyByPriority();
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

        /// <summary>
        /// Сигнал «курсор зашёл в hover-зону с этим индексом». Индекс соответствует позиции
        /// в массиве <see cref="CursorPack.CursorOnHover"/> (общий для всех наборов разрешений).
        /// Применение нового спрайта
        /// идёт сразу через <see cref="ApplyByPriority"/> с учётом приоритетов (LMB > RMB > Hover).
        ///
        /// Типичный источник вызова — <see cref="MouseHoverListener"/> на UI-элементах,
        /// но публичный API позволяет и программные сценарии (например, hover-зона
        /// в world-space без UGUI EventSystem).
        /// </summary>
        /// <param name="index">Индекс hover-варианта; <c>-1</c> = «нет hover».</param>
        public static void OnHover(int index = -1)
        {
            MouseKeys.HoverIndex.Set(index, Key);
            ApplyByPriority();
        }

        /// <summary>
        /// Сигнал «курсор покинул hover-зону с этим индексом». Защищён от гонки:
        /// если активный <see cref="MouseKeyMap.HoverIndex"/> уже не равен <paramref name="index"/>
        /// (другая зона перехватила hover в том же кадре), вызов игнорируется и текущий
        /// курсор сохраняется. Сценарий гонки типичен для вложенных hover-зон в UGUI,
        /// где EventSystem шлёт <c>OnPointerEnter</c> вложенной зоны до <c>OnPointerExit</c>
        /// родительской.
        /// </summary>
        /// <param name="index">Индекс зоны, из которой ушли; <c>-1</c> = «нет hover».</param>
        public static void OnUnHover(int index = -1)
        {
            if (MouseKeys.HoverIndex != index)
                return; //Опоздал и кто-то уже перехватил ховер в этом кадре (гонка)

            MouseKeys.HoverIndex.Set(-1, Key);
            ApplyByPriority();
        }

        /// <summary>
        /// Выбирает спрайт курсора по приоритету состояний и применяет его через <see cref="Apply"/>:
        /// 1) нажата LMB → <c>cursorLeftMouseDown</c>;
        /// 2) нажата RMB → <c>cursorRightMouseDown</c>;
        /// 3) активен hover → <c>cursorOnHover[HoverIndex]</c>;
        /// 4) ничего из перечисленного → <c>cursorDefault</c>.
        ///
        /// Fail-fast: некорректный <c>HoverIndex</c> (вне диапазона массива) бросает
        /// <see cref="IndexOutOfRangeException"/> — это сигнал об ошибке конфигурации,
        /// должен быть исправлен на этапе разработки, а не маскироваться.
        /// </summary>
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
        /// Применяет переданный спрайт к системному курсору. Hotspot курсора берётся из
        /// <see cref="Sprite.pivot"/> и конвертируется в пиксельные координаты с инверсией
        /// по Y (Unity Sprite использует bottom-left, <c>Cursor.SetCursor</c> — top-left).
        /// При <c>sprite == null</c> откатывается на <c>_cursorDefault</c>.
        /// </summary>
        /// <param name="sprite">Спрайт курсора. Если <c>null</c> — берётся дефолтный.</param>
        private static void Apply(Sprite sprite)
        {
            var texture = sprite?.texture ?? _cursorDefault.texture;
            // Конвертируем нормализованный pivot в пиксельные координаты для hotspot
            _hotspot = sprite?.pivot ?? _cursorDefault.pivot;
            _hotspot.y = texture.height - _hotspot.y;
            Cursor.SetCursor(texture, _hotspot, CursorMode.ForceSoftware);
        }
    }
}