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
        private static bool _hidden;

        private static CursorResolutionPack[] _packs;
        private static int _selectedIndex = -1;
        private static CursorHoverEntry _defaultSet;

        private static InputAction _leftMouseAction;
        private static InputAction _rightMouseAction;
        private static readonly object Key = new();

        /// <summary>
        /// Публичный наблюдаемый снимок текущего состояния мыши: нажатие LMB/RMB
        /// и активный hover-ключ. Поля — <see cref="Vortex.Core.Extensions.ReactiveValues.BoolData"/>
        /// и <see cref="Vortex.Core.Extensions.ReactiveValues.StringData"/>, можно подписаться
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
            MouseKeys.HoverKey.SetOwner(Key);
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
            ApplyByPriority(); // учитывает HideCursor базового набора

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
            var selectedIndex = -1;
            var largestIndex = -1;
            var bestMax = int.MaxValue;
            var largestMax = int.MinValue;

            for (var i = 0; i < packs.Length; i++)
            {
                var entry = packs[i];
                if (entry?.Pack == null)
                    continue;

                if (entry.MaxScreenHeight >= height && entry.MaxScreenHeight < bestMax)
                {
                    bestMax = entry.MaxScreenHeight;
                    selectedIndex = i;
                }

                if (entry.MaxScreenHeight > largestMax)
                {
                    largestMax = entry.MaxScreenHeight;
                    largestIndex = i;
                }
            }

            if (selectedIndex < 0)
                selectedIndex = largestIndex;

            _packs = packs;
            _selectedIndex = selectedIndex;
            _defaultSet = packs[selectedIndex].Pack.CursorDefault;
        }

        /// <summary>
        /// Перевыбор набора курсоров под текущее разрешение.
        /// Дёргать после применения видеонастроек (смена разрешения/режима окна).
        /// Состояние кнопок и ховера сохраняется — применяется через приоритеты
        /// </summary>
        public static void RefreshResolution()
        {
            var packs = Settings.Data().CursorPacks;
            if (packs == null || packs.Length == 0 || _defaultSet == null)
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
        /// Сигнал «курсор зашёл в hover-зону с этим ключом». Ключ совпадает с
        /// <see cref="CursorHoverEntry.Name"/>; вариант резолвится по всем пакетам с фолбэком
        /// (см. <see cref="ResolveHoverEntry"/>). Применение идёт сразу через <see cref="ApplyByPriority"/>.
        ///
        /// Типичный источник вызова — <see cref="MouseHoverListener"/> на UI-элементах,
        /// но публичный API позволяет и программные сценарии (например, hover-зона
        /// в world-space без UGUI EventSystem).
        /// </summary>
        /// <param name="key">Ключ hover-варианта; пусто/null = «нет hover».</param>
        public static void OnHover(string key = null)
        {
            MouseKeys.HoverKey.Set(key, Key);
            ApplyByPriority();
        }

        /// <summary>
        /// Сигнал «курсор покинул hover-зону с этим ключом». Защищён от гонки: если активный
        /// <see cref="MouseKeyMap.HoverKey"/> уже не равен <paramref name="key"/> (другая зона
        /// перехватила hover в том же кадре), вызов игнорируется и текущий курсор сохраняется.
        /// Сценарий гонки типичен для вложенных hover-зон в UGUI, где EventSystem шлёт
        /// <c>OnPointerEnter</c> вложенной зоны до <c>OnPointerExit</c> родительской.
        /// </summary>
        /// <param name="key">Ключ зоны, из которой ушли; пусто/null = «нет hover».</param>
        public static void OnUnHover(string key = null)
        {
            if (MouseKeys.HoverKey.Value != key)
                return; //Опоздал и кто-то уже перехватил ховер в этом кадре (гонка)

            MouseKeys.HoverKey.Set(null, Key);
            ApplyByPriority();
        }

        /// <summary>
        /// Применяет курсор по текущему состоянию через <see cref="Apply"/>: активна hover-зона →
        /// её вариант (с межпакетным фолбэком, см. <see cref="ResolveHoverEntry"/>), иначе базовый
        /// набор <c>CursorDefault</c>. И зона, и база разрешаются единым правилом нажатий
        /// (<see cref="ResolveHover"/>): LMB → Action, RMB → AltAction, иначе → Default.
        /// Индекс, не найденный ни в одном пакете (вплоть до первого) → базовый набор.
        /// </summary>
        private static void ApplyByPriority()
        {
            var key = MouseKeys.HoverKey.Value;
            var entry = string.IsNullOrEmpty(key) ? null : ResolveHoverEntry(key);
            entry ??= _defaultSet; // вне зоны или ключ нигде не найден — базовый набор
            if (entry == null)
                return; // контроллер не инициализирован

            // Набор может прятать системный курсор (под кастомный) — тогда спрайт не ставим.
            if (entry.HideCursor)
            {
                SetCursorHidden(true);
                return;
            }

            SetCursorHidden(false);
            Apply(ResolveHover(entry));
        }

        /// <summary>Скрыть/показать системный курсор (для наборов с HideCursor). Идемпотентно.</summary>
        private static void SetCursorHidden(bool hide)
        {
            if (_hidden == hide) return;
            _hidden = hide;
            Cursor.visible = !hide;
        }

        /// <summary>
        /// Резолв hover-варианта по ключу (<see cref="CursorHoverEntry.Name"/>) с межпакетным
        /// фолбэком: начиная с выбранного пакета и вниз по массиву к первому (пакеты авторятся по
        /// возрастанию MaxScreenHeight — первый = низкое разрешение). Ключ, отсутствующий в выбранном
        /// (более высоком) пакете, наследуется от более раннего; вверх фолбэка нет. <c>null</c>
        /// (ключ нигде не найден) → базовый набор.
        /// </summary>
        private static CursorHoverEntry ResolveHoverEntry(string key)
        {
            for (var i = _selectedIndex; i >= 0; i--)
            {
                var arr = _packs[i]?.Pack?.CursorOnHover;
                if (arr == null)
                    continue;

                foreach (var entry in arr)
                    if (entry != null && entry.Name == key)
                        return entry;
            }

            return null;
        }

        /// <summary>
        /// Спрайт для активной hover-зоны с учётом нажатий: LMB → <see cref="CursorHoverEntry.Action"/>,
        /// RMB → <see cref="CursorHoverEntry.AltAction"/>, иначе → <see cref="CursorHoverEntry.Default"/>.
        /// Незаполненное action-поле откатывается на Default набора; пустой Default — на дефолт
        /// базового набора (это делает <see cref="Apply"/> по <c>null</c>).
        /// </summary>
        private static Sprite ResolveHover(CursorHoverEntry entry)
        {
            Sprite sprite = null;
            if (MouseKeys.LeftKeyPressed)
                sprite = entry.Action;
            else if (MouseKeys.RightKeyPressed)
                sprite = entry.AltAction;

            return sprite != null ? sprite : entry.Default; // Default == null → Apply даст базовый дефолт
        }

        /// <summary>
        /// Применяет переданный спрайт к системному курсору. Hotspot курсора берётся из
        /// <see cref="Sprite.pivot"/> и конвертируется в пиксельные координаты с инверсией
        /// по Y (Unity Sprite использует bottom-left, <c>Cursor.SetCursor</c> — top-left).
        /// При <c>sprite == null</c> откатывается на Default базового набора (<c>_defaultSet.Default</c>).
        /// </summary>
        /// <param name="sprite">Спрайт курсора. Если <c>null</c> — берётся дефолтный.</param>
        private static void Apply(Sprite sprite)
        {
            var texture = sprite?.texture ?? _defaultSet.Default.texture;
            // Конвертируем нормализованный pivot в пиксельные координаты для hotspot
            _hotspot = sprite?.pivot ?? _defaultSet.Default.pivot;
            _hotspot.y = texture.height - _hotspot.y;
            Cursor.SetCursor(texture, _hotspot, CursorMode.ForceSoftware);
        }
    }
}