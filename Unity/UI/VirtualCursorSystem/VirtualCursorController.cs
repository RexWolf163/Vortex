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
            IsReady = false;
        }

        /// <summary>Пересчитать вид после смены разрешения/режима окна (тир мог смениться).</summary>
        public static void RefreshResolution() => Recompute();

        // --- Интейк источников (internal — зовут драйверы/зоны пакета) ---

        /// <summary>Репорт позиции от источника. Last-source-wins: репортящий становится активным.</summary>
        internal static void ReportPointer(Vector2 screen, PointerSourceKind source)
        {
            if (_model == null) return;
            _model.ScreenPosition.Set(screen, Key);
            _model.ActiveSource.Set(source, Key);
        }

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

        private static void Recompute()
        {
            if (_model == null) return;
            var visual = CursorSkinResolver.Resolve(
                _settings,
                CursorSkinSelector.Selected.Value,
                _model.HoverKey.Value,
                _model.Actions.Value,
                Screen.height);
            if (_pointerHidden && !visual.Hide) // активный источник скрывает курсор — форсим Hide поверх резолва
                visual = new CursorVisual(visual.Sprite, visual.Hotspot, true);
            _visual.Set(visual, Key);
        }
    }
}
