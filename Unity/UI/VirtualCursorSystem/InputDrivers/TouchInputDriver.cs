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
        /// Касание только скрывает курсор: репортится текущая позиция курсора (без сдвига), значение экшена
        /// не читается — биндинг может быть любым (Button или Value). Клик обрабатывает UGUI нативно.
        /// </summary>
        HideOnly,

        /// <summary>
        /// Курсор прыгает в точку касания и скрывается. Биндинг — Value/Vector2 позиции касания
        /// (например, <c>&lt;Touchscreen&gt;/primaryTouch/position</c>).
        /// </summary>
        AbsolutePosition,

        /// <summary>
        /// Трекпад: дельта пальца сдвигает курсор, курсор виден. Биндинг — Value/Vector2 дельты
        /// (например, <c>&lt;Touchscreen&gt;/delta</c>).
        /// </summary>
        Delta,
    }

    /// <summary>
    /// Платформенный фильтр подключения драйвера касания: при несовпадении платформы загрузчик драйвер
    /// не подключает. Нужен, чтобы развести экземпляры пары HideOnly + Delta по платформам — иначе на мобильных
    /// Delta (не скрывает) перебивал бы HideOnly по правилу last-source-wins.
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

    /// <summary>Проверка платформы по <see cref="TouchPlatformFilter"/> (драйвер касания, UGUI-модуль пакета).</summary>
    public static class TouchPlatformFilterExtensions
    {
        /// <summary>Платформа подходит под фильтр.</summary>
        public static bool Matches(this TouchPlatformFilter filter, RuntimePlatform platform)
        {
            switch (filter)
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
    }

    /// <summary>
    /// Драйвер касания. Три режима работы (см. <see cref="TouchDriverMode"/>):
    /// <list type="bullet">
    /// <item><b>HideOnly</b> (дефолт, Android): скрываем визуал; репортится текущая позиция курсора (без сдвига) —
    /// только чтобы передать флаг скрытия.</item>
    /// <item><b>AbsolutePosition</b>: репорт точки касания.</item>
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

        /// <summary>Экшен касания (null — не разрезолвлен).</summary>
        private InputAction _action;

        /// <summary>
        /// В Delta курсор остаётся видимым — пользователь смотрит на экранный курсор, не на палец.
        /// В HideOnly/AbsolutePosition — скрываем (прямой контакт, курсор семантически не нужен).
        /// </summary>
        public override bool HidesCursor => mode != TouchDriverMode.Delta;

        /// <summary>Поддержка платформы по <see cref="platformFilter"/>.</summary>
        public override bool SupportsPlatform(RuntimePlatform platform) => platformFilter.Matches(platform);

        /// <summary>Резолв экшена касания и подписка на него (id не проверяется).</summary>
        public override void Connect()
        {
            _action = ResolveAction(pointActionId);
            SubscribeAction(pointActionId, OnPoint, null);
        }

        /// <summary>Отписка от экшена касания.</summary>
        public override void Disconnect()
        {
            UnsubscribeAction(pointActionId);
            _action = null;
        }

        /// <summary>Событие касания — обработка по <see cref="mode"/>.</summary>
        private void OnPoint()
        {
            if (_action == null) return;

            switch (mode)
            {
                case TouchDriverMode.HideOnly:
                    // Значение экшена НЕ читаем — биндинг может быть Button (primaryTouch),
                    // ReadValue<Vector2> на нём выбросит control-type mismatch. Нужен только факт касания.
                    // Репортим текущую позицию курсора: позиция не меняется (дедуп ReactiveValue), уходит только
                    // флаг HidesCursor — мышь для этого не нужна.
                    Report(VirtualCursorBus.Data.ScreenPosition.Value);
                    break;

                case TouchDriverMode.AbsolutePosition:
                {
                    var pos = _action.ReadValue<Vector2>();
                    Report(pos);
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
                    Report(cursor);
                    break;
                }
            }
        }
    }
}