using System;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.InputSystem;
using Vortex.Unity.EditorTools.Attributes;
using Vortex.Unity.UI.VirtualCursorSystem.Bus;

namespace Vortex.Unity.UI.VirtualCursorSystem.InputDrivers
{
    /// <summary>
    /// Варианты обработки касания виртуальным курсором. Разные платформы хотят разного:
    /// Android (нативный UGUI сам разруливает тач) — только спрятать визуал; десктоп с
    /// тач-экраном — трекпад-смещение; специализированный киоск без мыши — абсолютная позиция.
    /// </summary>
    public enum TouchDriverMode
    {
        /// <summary>
        /// Android-профиль. На касание выставляем источник <see cref="PointerSourceKind.Point"/> +
        /// hide-флаг, но <c>ScreenPosition</c> НЕ трогаем. UGUI (через <c>InputSystemUIInputModule</c>
        /// на <c>&lt;Touchscreen&gt;</c>) обрабатывает клик нативно; <see cref="VirtualPointerDispatcher"/>
        /// не триггерится (нет изменения позиции) — двойных кликов и лишнего raycast'а нет.
        /// <b>Биндинг:</b> любой — Button (например, <c>&lt;Touchscreen&gt;/primaryTouch</c>) или
        /// Value — значение не читается, важен только факт события.
        /// </summary>
        HideOnly,

        /// <summary>
        /// Абсолютная позиция (устаревшее поведение). Report'им точку касания, курсор прыгает к пальцу.
        /// Полезно только там, где нативный UGUI-пайплайн тача выключен, а виртуальный диспетчер —
        /// единственный источник кликов (киоск/VR-туториал на тач-поверхности). HidesCursor=true —
        /// касание скрывает визуал (как в старой реализации).
        /// <b>Биндинг:</b> Value/Vector2 позиции касания (например, <c>&lt;Touchscreen&gt;/primaryTouch/position</c>).
        /// </summary>
        AbsolutePosition,

        /// <summary>
        /// Трекпад-смещение. Delta пальца добавляется к текущей позиции курсора. Курсор остаётся
        /// видимым (HidesCursor=false): пользователь смотрит на экранный курсор, не на палец.
        /// Delta берётся напрямую из биндинга — Unity InputSystem сам обнуляет её между касаниями
        /// (при lift accumulation не продолжается).
        /// <b>Биндинг:</b> Value/Vector2 delta касания (например, <c>&lt;Touchscreen&gt;/delta</c>).
        /// </summary>
        Delta,
    }

    /// <summary>
    /// Платформенный фильтр подключения драйвера касания. На момент <c>Connect</c>
    /// <see cref="CursorInputLoader"/> спрашивает <see cref="InputDriver.SupportsPlatform"/> —
    /// при <c>false</c> драйвер пропускается и в рантайме не фаерит.
    ///
    /// Зачем это нужно: типичная пара TouchInputDriver'ов (HideOnly + Delta) в одном
    /// <see cref="InputDriverSet"/> даёт конфликт на последнем-источнике-wins. Delta фаерит
    /// каждый кадр движения пальца с <c>HidesCursor=false</c>, HideOnly — только единожды
    /// на tap-down с <c>HidesCursor=true</c>. При свайпе Delta всегда перезаписывает hide
    /// в <c>false</c> → курсор виден на всех платформах. Фильтр разводит экземпляры по
    /// платформам (HideOnly только на мобильном, Delta — только на десктопе).
    /// </summary>
    public enum TouchPlatformFilter
    {
        /// <summary>Любая платформа (дефолт — обратная совместимость, не рекомендуется для парного сета).</summary>
        All,

        /// <summary>Только Android и iOS runtime. В редакторе НЕ подключается — тестить на устройстве.</summary>
        MobileOnly,

        /// <summary>Standalone Windows/Mac/Linux + все редакторы (считаются desktop'ами).</summary>
        DesktopOnly,
    }

    /// <summary>
    /// Драйвер касания. Три режима работы (см. <see cref="TouchDriverMode"/>):
    /// <list type="bullet">
    /// <item><b>HideOnly</b> (дефолт, Android): скрываем визуал, позицию не репортим.</item>
    /// <item><b>AbsolutePosition</b>: прежнее поведение — репорт точки касания.</item>
    /// <item><b>Delta</b>: трекпад — палец двигает курсор относительно; курсор виден.</item>
    /// </list>
    /// <see cref="HidesCursor"/> = <c>true</c> для HideOnly/AbsolutePosition (касание — прямой контакт,
    /// курсор не нужен), <c>false</c> для Delta (пользователь смотрит на курсор).
    ///
    /// Для desktop-with-touchscreen + Android обычно нужны ДВА экземпляра драйвера в
    /// <c>InputDriverSet</c>: один в режиме HideOnly (биндинг на <c>primaryTouch</c>) — чтобы
    /// на Android гасить курсор, второй в режиме Delta (биндинг на <c>Touchscreen/delta</c>) —
    /// чтобы на десктопе палец работал как трекпад.
    /// </summary>
    [Serializable]
    public class TouchInputDriver : InputDriver
    {
        [SerializeField, Tooltip("Режим обработки касания. HideOnly — Android (UGUI сам обработает " +
                                 "клик, нам только спрятать курсор). AbsolutePosition — курсор прыгает " +
                                 "в точку касания (киоск). Delta — трекпад: палец двигает курсор " +
                                 "относительно, курсор видим.")]
        private TouchDriverMode mode = TouchDriverMode.HideOnly;

        [SerializeField, Tooltip("Фильтр платформ. All — любая (дефолт, не рекомендуется для парного " +
                                 "сета HideOnly+Delta). MobileOnly — только Android/iOS (не редактор). " +
                                 "DesktopOnly — Standalone + все редакторы. Разводит экземпляры по " +
                                 "платформам, чтобы Delta не перебивал HideOnly на Android (см. summary " +
                                 "у TouchPlatformFilter).")]
        private TouchPlatformFilter platformFilter = TouchPlatformFilter.All;

        [SerializeField, ValueSelector("GetInputActions"),
         Tooltip("Action id касания. HideOnly: любой тач-экшен (значение не читается). " +
                 "AbsolutePosition: Value/Vector2 позиции (Touchscreen/primaryTouch/position). " +
                 "Delta: Value/Vector2 дельты (Touchscreen/delta).")]
        private string pointActionId;

        [SerializeField, ShowIf(nameof(mode), TouchDriverMode.AbsolutePosition),
         Tooltip("Варпать ОС-мышь в точку касания (только для AbsolutePosition). На чистом Android " +
                 "(Mouse.current == null) — безвредный no-op.")]
        private bool warpSystemMouse = true;

        [SerializeField, ShowIf(nameof(mode), TouchDriverMode.Delta), Min(0f),
         Tooltip("Множитель чувствительности в режиме Delta. 1 = трекпад 1:1 (палец на N px → " +
                 "курсор на N px). >1 — ускорение (меньше сдвигов пальцем для того же пути).")]
        private float deltaSpeedMultiplier = 1f;

        private InputAction _action;

        // В Delta курсор остаётся видимым — пользователь смотрит на экранный курсор, не на палец.
        // В HideOnly/AbsolutePosition — скрываем (прямой контакт, курсор семантически не нужен).
        public override bool HidesCursor => mode != TouchDriverMode.Delta;

        public override bool SupportsPlatform(RuntimePlatform platform)
        {
            switch (platformFilter)
            {
                case TouchPlatformFilter.MobileOnly:
                    // Android и iOS runtime. Редакторы НЕ входят — для тестирования строить на устройство.
                    return platform == RuntimePlatform.Android
                           || platform == RuntimePlatform.IPhonePlayer;

                case TouchPlatformFilter.DesktopOnly:
                    // Standalone-плееры + ВСЕ редакторы (редактор всегда десктоп по хосту —
                    // Application.platform возвращает *Editor независимо от Build Target).
                    return platform == RuntimePlatform.WindowsPlayer
                           || platform == RuntimePlatform.OSXPlayer
                           || platform == RuntimePlatform.LinuxPlayer
                           || platform == RuntimePlatform.WindowsEditor
                           || platform == RuntimePlatform.OSXEditor
                           || platform == RuntimePlatform.LinuxEditor;

                default:
                    return true;
            }
        }

        public override void Connect()
        {
            _action = ResolveAction(pointActionId);
            EnableMap(pointActionId);
            SubscribeAction(pointActionId, OnPoint, null);
        }

        public override void Disconnect()
        {
            UnsubscribeAction(pointActionId);
            DisableMap(pointActionId);
            _action = null;
        }

        private void OnPoint()
        {
            if (_action == null) return;

            switch (mode)
            {
                case TouchDriverMode.HideOnly:
                    // Значение экшена НЕ читаем — биндинг может быть Button (primaryTouch),
                    // ReadValue<Vector2> на нём выбросит control-type mismatch. Нужен только факт
                    // события: пометить активный источник + hide-канал. ScreenPosition не трогаем.
                    VirtualCursorController.SetActiveSource(PointerSourceKind.Point, HidesCursor);
                    break;

                case TouchDriverMode.AbsolutePosition:
                {
                    var pos = _action.ReadValue<Vector2>();
                    Report(pos, PointerSourceKind.Point);
                    if (warpSystemMouse)
                        Mouse.current?.WarpCursorPosition(pos);
                    break;
                }

                case TouchDriverMode.Delta:
                {
                    // Биндинг на Touchscreen/delta: InputSystem сам отдаёт per-frame дельту пальца
                    // и обнуляет её при отрыве. Накопление между касаниями не наше дело —
                    // гарантируется источником данных.
                    var delta = _action.ReadValue<Vector2>();
                    if (delta.sqrMagnitude < 0.0001f || VirtualCursorBus.Data == null)
                        break;

                    delta *= deltaSpeedMultiplier;
                    var cursor = VirtualCursorBus.Data.ScreenPosition.Value + delta;
                    cursor.x = Mathf.Clamp(cursor.x, 0f, Screen.width - 1f);
                    cursor.y = Mathf.Clamp(cursor.y, 0f, Screen.height - 1f);
                    Report(cursor, PointerSourceKind.Point);
                    break;
                }
            }
        }
    }
}
