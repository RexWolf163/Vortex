using System;
using UnityEngine;

namespace Vortex.Unity.UI.VirtualCursorSystem
{
    /// <summary>
    /// Статический контроллер пакета: владеет <see cref="PointerModel"/>, резолвит <see cref="Visual"/>
    /// из выбранной темы/hover/действий/разрешения, ведёт реестр камер для проекции (partial-файл).
    /// Инициализируется бутстрапом (<see cref="Init"/>) с конфигом скинов.
    /// Расхождение с Singleton-каноном осознанно — по прецеденту CursorController (тоже static): одна
    /// реализация, свап стратегии не нужен, per-frame Update не требуется (интеграция скорости — в драйвере).
    /// </summary>
    public static partial class VirtualCursorController
    {
        private static readonly object Key = new();

        private static PointerModel _model;
        private static CursorVisualData _visual;
        private static CursorSkinSettings _settings;

        /// <summary>
        /// Скрыт ли курсор из-за активного источника (касание — прямой контакт, курсор не нужен).
        /// Ставится в <see cref="ReportPointer(Vector2, PointerSourceKind, bool)"/> по last-source-wins и
        /// подмешивается в <see cref="Recompute"/> поверх результата резолвера (форсит Hide).
        /// </summary>
        private static bool _pointerHidden;

        /// <summary>
        /// Независимый внешний канал скрытия, управляемый <see cref="HideCursor"/>/<see cref="ShowCursor"/>.
        /// Не сбрасывается репортами от драйверов — применяется поверх <see cref="_pointerHidden"/> и
        /// скина через OR в <see cref="Recompute"/>. Подходит, когда внешнему коду нужно скрыть
        /// курсор на время (меню, катсцена, фокус-навигация) без зависимости от last-source-wins.
        /// </summary>
        private static bool _externalHidden;

        public static bool IsReady { get; private set; }

        // internal — реактивная модель/вид доступны наружу ТОЛЬКО через read-only фасад VirtualCursorBus.
        internal static PointerModel Model => _model;
        internal static CursorVisualData Visual => _visual;

        public static event Action OnReady;

        /// <summary>Инициализация с конфигом скинов. Идемпотентна (повторный вызов игнорируется до Cleanup).</summary>
        public static void Init(CursorSkinSettings settings)
        {
            if (IsReady)
                return;

            if (settings == null)
                Debug.LogError("[VirtualCursor] Init: settings=null — CursorSkinSettings не передан, " +
                               "курсор не будет отрисован (Resolve→None). Проверь VirtualCursorBootstrap.");

            _settings = settings;
            _model = new PointerModel();
            _model.SetOwner(Key);
            _visual = new CursorVisualData(CursorVisual.None, Key);

            // Старт темы — дефолт из конфига, если ещё ничего не выбрано (persist проекта может уже задать).
            if (string.IsNullOrEmpty(CursorSkinSelector.Selected.Value) && settings != null)
                CursorSkinSelector.Select(settings.DefaultSetKey);

            _model.HoverKey.OnUpdateData += Recompute;
            _model.Actions.OnUpdateData += Recompute;
            CursorSkinSelector.Selected.OnUpdateData += Recompute;

            Recompute();

            IsReady = true;
            OnReady?.Invoke();
        }

        /// <summary>Сброс (для рестарта без выгрузки домена).</summary>
        public static void Cleanup()
        {
            if (_model != null)
            {
                _model.HoverKey.OnUpdateData -= Recompute;
                _model.Actions.OnUpdateData -= Recompute;
            }

            CursorSkinSelector.Selected.OnUpdateData -= Recompute;
            _cameras.Clear();
            _model = null;
            _visual = null;
            _settings = null;
            _pointerHidden = false;
            _externalHidden = false;
            IsReady = false;
        }

        /// <summary>Пересчитать вид после смены разрешения/режима окна (тир мог смениться).</summary>
        public static void RefreshResolution() => Recompute();

        /// <summary>
        /// Внешний запрос «скрыть курсор». Независимый канал поверх <see cref="_pointerHidden"/> и скина;
        /// не сбрасывается репортами от драйверов. Идемпотентно: повторный вызов без эффекта.
        /// Снимается только <see cref="ShowCursor"/>.
        /// </summary>
        public static void HideCursor()
        {
            if (_externalHidden) return;
            _externalHidden = true;
            Recompute();
        }

        /// <summary>
        /// Снимает внешний запрос скрытия (<see cref="HideCursor"/>). Это обнуление ТОЛЬКО своего
        /// канала: если курсор скрыт скином (<c>CursorSkin.HideCursor</c>) или источником
        /// (<see cref="_pointerHidden"/>) — он останется скрыт. Идемпотентно.
        /// </summary>
        public static void ShowCursor()
        {
            if (!_externalHidden) return;
            _externalHidden = false;
            Recompute();
        }

        /// <summary>Текущее состояние внешнего канала скрытия (для диагностики / потребителей).</summary>
        public static bool IsCursorHiddenExternally => _externalHidden;

        // --- Интейк источников (internal — зовут драйверы/зоны пакета) ---

        /// <summary>
        /// Репорт позиции от источника. Last-source-wins: репортящий становится активным.
        /// Делегирует в 3-арг с <c>hidesCursor:false</c> — источник без явного hide считается
        /// показывающим курсор, поэтому смена источника через эту перегрузку корректно снимает
        /// устаревший source-hide (напр. после касания в HideOnly), как обещает контракт 3-арг.
        /// </summary>
        internal static void ReportPointer(Vector2 screen, PointerSourceKind source) =>
            ReportPointer(screen, source, false);

        /// <summary>
        /// Репорт позиции с флагом скрытия курсора для этого источника (касание → true). Last-source-wins:
        /// активный источник задаёт и позицию, и видимость — смена источника корректно возвращает курсор.
        /// </summary>
        internal static void ReportPointer(Vector2 screen, PointerSourceKind source, bool hidesCursor)
        {
            if (_model == null) return;
            _model.ScreenPosition.Set(screen, Key);
            _model.ActiveSource.Set(source, Key);
            if (_pointerHidden == hidesCursor) return;
            _pointerHidden = hidesCursor;
            Recompute();
        }

        /// <summary>
        /// Переключить активный источник + hide-флаг БЕЗ изменения <c>ScreenPosition</c>. Нужен для
        /// драйверов, которые должны отметиться как «активный источник» (и, например, скрыть курсор),
        /// но не двигать виртуальный pointer — чтобы не триггерить <c>VirtualPointerDispatcher</c>
        /// лишним raycast'ом и не конфликтовать с нативным обработчиком того же устройства
        /// (сценарий: Android + <c>InputSystemUIInputModule</c> на touchscreen — клик по кнопке
        /// отрабатывает нативным путём, нам достаточно только спрятать визуал курсора).
        /// </summary>
        internal static void SetActiveSource(PointerSourceKind source, bool hidesCursor)
        {
            if (_model == null) return;
            _model.ActiveSource.Set(source, Key);
            if (_pointerHidden == hidesCursor) return;
            _pointerHidden = hidesCursor;
            Recompute();
        }

        internal static void SetAction(PointerAction action, bool active)
        {
            if (_model == null) return;
            _model.Actions.Set(_model.Actions.Value.Set(action, active), Key);
        }

        internal static void ClearActions()
        {
            if (_model == null) return;
            _model.Actions.Set(PointerActionMask.Empty, Key);
        }

        internal static void SetHover(string key)
        {
            if (_model == null) return;
            _model.HoverKey.Set(key ?? string.Empty, Key);
        }

        internal static void SetOverUI(bool overUI)
        {
            if (_model == null) return;
            _model.IsOverUI.Set(overUI, Key);
        }

        /// <summary>
        /// Репорт импульса скролла (пикс/тик, X — горизонталь, Y — вертикаль, соглашение UGUI).
        /// Пушится драйвером (<c>ScrollInputDriver</c>) на каждый тик колеса/клавиши направления;
        /// <c>VirtualPointerDispatcher</c> подписан на <c>ScrollDelta.OnUpdate</c> и за одно событие
        /// делает raycast + <c>ExecuteEvents.scrollHandler</c>. Нулевая дельта в тик — обычно без
        /// смысла, драйвер отбрасывает сам; тут гарда не делаем, чтобы не прятать осознанный Set(0).
        /// </summary>
        internal static void ReportScroll(Vector2 delta)
        {
            if (_model == null) return;
            _model.ScrollDelta.Set(delta, Key);
        }

        private static void Recompute()
        {
            if (_model == null) return;
            var visual = CursorSkinResolver.Resolve(
                _settings,
                CursorSkinSelector.Selected.Value,
                _model.HoverKey.Value,
                _model.Actions.Value,
                Screen.height);
            // Любой из трёх каналов (скин / активный источник / внешний запрос) форсит Hide (OR-композиция):
            // скин — "курсор по дизайну прячется в этом состоянии"; _pointerHidden — "активный источник не
            // показывает курсор (касание/фокус)"; _externalHidden — "внешний код скрыл явно".
            if ((_pointerHidden || _externalHidden) && !visual.Hide)
                visual = new CursorVisual(visual.Sprite, visual.Hotspot, true);
            _visual.Set(visual, Key);
        }
    }
}
