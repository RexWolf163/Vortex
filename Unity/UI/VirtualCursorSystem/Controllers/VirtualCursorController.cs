using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Vortex.Unity.UI.VirtualCursorSystem.Bus;
using Vortex.Unity.UI.VirtualCursorSystem.Model;

namespace Vortex.Unity.UI.VirtualCursorSystem.Controllers
{
    /// <summary>
    /// Контроллер виртуального курсора — единственный писатель <see cref="PointerModel"/>.
    /// Принимает репорты драйверов ввода (позиция с флагом скрытия, действия, скролл) и сводит
    /// итоговый скин курсора: скрытие от последнего источника репорта имеет высший приоритет,
    /// иначе — верхний запрос из LIFO-стека запросов скинов (<see cref="OnSkin"/>/<see cref="OffSkin"/>).
    /// </summary>
    public static partial class VirtualCursorController
    {
        /// <summary>Ключ владельца реактивных полей модели.</summary>
        private static readonly object Key = new();

        /// <summary>
        /// Защита от повторного входа в <see cref="ReportPointer"/>: вызов из подписчика
        /// <c>ScreenPosition.OnUpdate</c> во время текущего репорта игнорируется.
        /// </summary>
        private static bool _computeDriverReporting = false;

        /// <summary>
        /// Курсор скрыт: ставится репортом скрывающего источника, снимается значимым сдвигом нескрывающего
        /// (см. <see cref="ReportPointer"/>).
        /// </summary>
        private static bool _hidden = false;

        /// <summary>Модель шины, которой владеет контроллер.</summary>
        private static PointerModel _model;

        /// <summary>
        /// Привязка к модели шины и захват владения её реактивными полями.
        /// Контроллер единственный, поэтому инициализация безусловная на старте приложения.
        /// </summary>
        [RuntimeInitializeOnLoadMethod]
        private static void Init()
        {
            _model = VirtualCursorBus.Data;
            _model.SetOwner(Key);
            VirtualCursorBus.Gesture.SetOwner(Key);
        }

        /// <summary>Запрос скина от конкретного владельца.</summary>
        private class SkinOwner
        {
            /// <summary>Запрошенное имя скина.</summary>
            public string SkinName;

            /// <summary>Владелец запроса; у одного владельца не больше одного запроса.</summary>
            public Object Owner;
        }

        /// <summary>LIFO-стек запросов скинов: действует верхний (последний добавленный).</summary>
        private static List<SkinOwner> skinsLIFO = new();

        /// <summary>
        /// Запросить скин курсора — например, по наведению на UI-элемент (<see cref="Handlers.CursorHoverZone"/>).
        /// Прежний запрос этого владельца снимается, новый кладётся на верх LIFO-стека; итоговый скин
        /// пересчитывается с учётом скрытия (скрытый курсор остаётся скрытым).
        /// Если рендер не знает имени скина, он показывает свой скин по умолчанию.
        /// </summary>
        /// <param name="owner">Модель курсора (цель расширения).</param>
        /// <param name="skin">Имя запрашиваемого скина.</param>
        /// <param name="skinOwner">Владелец запроса — по нему запрос снимается в <see cref="OffSkin"/>.</param>
        public static void OnSkin(this PointerModel owner, string skin, Object skinOwner)
        {
            for (var i = skinsLIFO.Count - 1; i >= 0; i--)
            {
                if (skinsLIFO[i].Owner != skinOwner)
                    continue;
                skinsLIFO.RemoveAt(i);
            }

            skinsLIFO.Add(new SkinOwner { SkinName = skin, Owner = skinOwner });
            owner.Skin.Set(GetSkinName(), Key);
        }

        /// <summary>
        /// Снять запрос скина владельца и пересчитать итоговый скин (следующий в стеке или базовый).
        /// Без активного запроса — ничего не меняет.
        /// </summary>
        /// <param name="owner">Модель курсора (цель расширения).</param>
        /// <param name="skinOwner">Владелец снимаемого запроса.</param>
        public static void OffSkin(this PointerModel owner, Object skinOwner)
        {
            for (var i = skinsLIFO.Count - 1; i >= 0; i--)
            {
                if (skinsLIFO[i].Owner != skinOwner)
                    continue;
                skinsLIFO.RemoveAt(i);
            }

            owner.Skin.Set(GetSkinName(), Key);
        }

        /// <summary>
        /// Итоговый скин: <see cref="VirtualCursorBus.HideCursor"/> при скрытии, иначе верх стека запросов,
        /// иначе пустая строка (базовый скин рендера).
        /// </summary>
        private static string GetSkinName()
        {
            if (_hidden)
                return VirtualCursorBus.HideCursor;

            return skinsLIFO.Count == 0 ? string.Empty : skinsLIFO[^1].SkinName;
        }

        /// <summary>
        /// Радиус дрейфа, квадрат пикселей (5 px): накопленный от центра дрейфа сдвиг больше — значимый.
        /// </summary>
        private const float DriftRadiusSqr = 25f;

        /// <summary>
        /// Шаг, квадрат пикселей (3 px): сдвиг за один репорт больше — значимый. Выше ошибки эха варпа ОС-мыши
        /// (округление до пикселя: ≈0.7 px, на HiDPI ≈1.4 px) — эхо значимым не бывает без фильтров в драйверах.
        /// </summary>
        private const float DriftStepSqr = 9f;

        /// <summary>Центр дрейфа: позиция на момент последнего значимого сдвига.</summary>
        private static Vector2 _driftCenter;

        /// <summary>Позиция прошлого репорта.</summary>
        private static Vector2 _lastPoint;

        /// <summary>
        /// Источник прошлого репорта позиции — устройство ввода; <c>null</c> — перенос без устройства (фокус). Смена
        /// источника переякоривает фильтр дрейфа (<see cref="ReportPointer"/>).
        /// </summary>
        private static InputDevice _driftSource;

        /// <summary>
        /// Репорт позиции с флагом скрытия курсора для этого источника. Позиция пишется всегда; фильтр дрейфа
        /// (<see cref="IsSignificantMove"/>) решает, значим ли сдвиг.
        ///
        /// Фильтр накапливает сдвиг только в пределах одного источника: репорт с другого устройства (или без него)
        /// незначим и переякоривает фильтр — центр дрейфа переносится в его точку. Иначе источник получал бы в зачёт
        /// накопление чужого: эхо варпа ОС-мыши (с ошибкой округления до пикселя ОС — при масштабированном окне до
        /// ~2 px по оси) добирало бы порог за медленным стиком и засчитывалось мыши.
        /// <list type="bullet">
        /// <item>скрывающий источник скрывает курсор на любом репорте;</item>
        /// <item>нескрывающий возвращает курсор только значимым сдвигом — дрожание и эхо варпа ОС-мыши скрытие не снимают;</item>
        /// <item>значимый сдвиг поднимает <see cref="PointerModel.OnPointerMoved"/> (после позиции и скина);</item>
        /// <item>устройство источника уходит в <see cref="PointerModel.LastDevice"/> на репорте скрывающего источника
        /// и на значимом сдвиге (<see cref="ReportDevice"/>).</item>
        /// </list>
        /// Повторный вход (из подписчика во время репорта) игнорируется. Гард снимается в finally —
        /// исключение у подписчика позиции или скина не замораживает курсор навсегда.
        /// </summary>
        /// <param name="screen">Позиция в пикселях экрана.</param>
        /// <param name="hidesCursor">Скрывает ли курсор этот источник.</param>
        /// <param name="device">Устройство ввода источника; <c>null</c> — перенос не от пользователя (фокус).</param>
        internal static void ReportPointer(Vector2 screen, bool hidesCursor = false, InputDevice device = null)
        {
            if (_computeDriverReporting) return;
            _computeDriverReporting = true;
            try
            {
                var sourceChanged = device != _driftSource;
                _driftSource = device;
                var moved = !sourceChanged && IsSignificantMove(screen);
                _lastPoint = screen;
                if (moved || sourceChanged) _driftCenter = screen;

                if (_model.ScreenPosition.Value != screen)
                    InvalidateProjection();
                _model.ScreenPosition.Set(screen, Key);

                if (hidesCursor) _hidden = true;
                else if (moved) _hidden = false;

                _model.Skin.Set(GetSkinName(), Key);

                if (hidesCursor || moved) ReportDevice(device);

                if (moved) _model.RaisePointerMoved();
            }
            finally
            {
                _computeDriverReporting = false;
            }
        }

        /// <summary>
        /// Репорт устройства пользовательского ввода в <see cref="PointerModel.LastDevice"/> (layout устройства).
        /// Драйвер зовёт его на нажатии экшена; для движения указателя — <see cref="ReportPointer"/> по решению фильтра
        /// дрейфа. <c>null</c> — ничего не меняет; тот же layout — без события (дедуп реактивного поля).
        /// </summary>
        internal static void ReportDevice(InputDevice device)
        {
            if (device == null) return;
            _model.LastDevice.Set(device.layout, Key);
        }

        /// <summary>
        /// Фильтр дрейфа: сдвиг значим, если за один репорт больше <see cref="DriftStepSqr"/> или от центра дрейфа
        /// больше <see cref="DriftRadiusSqr"/>. Быстрое движение значимо на каждом шаге, медленное — по накоплении радиуса,
        /// дрожание на месте — никогда. Зовётся только для репорта того же источника, что и прошлый.
        /// </summary>
        private static bool IsSignificantMove(Vector2 screen) =>
            (screen - _lastPoint).sqrMagnitude > DriftStepSqr ||
            (screen - _driftCenter).sqrMagnitude > DriftRadiusSqr;

        /// <summary>
        /// Держатели действий по индексу <see cref="PointerAction"/>: источники, удерживающие действие сейчас.
        /// Бит маски активен, пока держатель есть хотя бы один.
        /// </summary>
        private static readonly HashSet<object>[] ActionHolders = CreateActionHolders();

        /// <summary>Пустые наборы держателей — по одному на значение <see cref="PointerAction"/>.</summary>
        private static HashSet<object>[] CreateActionHolders()
        {
            var holders = new HashSet<object>[System.Enum.GetValues(typeof(PointerAction)).Length];
            for (var i = 0; i < holders.Length; i++)
                holders[i] = new HashSet<object>();
            return holders;
        }

        /// <summary>
        /// Взять или отпустить действие от имени источника; бит в маске <c>Actions</c> активен, пока его держит хотя бы
        /// один источник (два экшена или два пальца на одно действие — отпускание одного не снимает действие).
        /// Идемпотентно по источнику: повторное взятие и отпускание неудерживаемого ничего не меняют.
        /// Остальные биты не трогаются.
        /// </summary>
        /// <param name="action">Действие.</param>
        /// <param name="active"><c>true</c> — взять, <c>false</c> — отпустить.</param>
        /// <param name="source">Источник (привязка драйвера, палец) — ключ удержания.</param>
        internal static void SetAction(PointerAction action, bool active, object source)
        {
            var holders = ActionHolders[(int)action];
            if (active) holders.Add(source);
            else holders.Remove(source);

            _model.Actions.Set(_model.Actions.Value.Set(action, holders.Count > 0), Key);
        }

        /// <summary>
        /// Репорт кадра жеста мультикасания в <c>VirtualCursorBus.Gesture.Frame</c>. Кадр, равный предыдущему,
        /// публикуется принудительно — это новый импульс, а не повтор состояния.
        /// </summary>
        internal static void ReportGesture(GestureFrame frame)
        {
            var data = VirtualCursorBus.Gesture.Frame;
            if (data.Value.Equals(frame))
                data.ForceUpdate();
            else
                data.Set(frame, Key);
        }

        /// <summary>Снять все действия в маске <c>Actions</c> вместе с их держателями.</summary>
        internal static void ClearActions()
        {
            foreach (var holders in ActionHolders)
                holders.Clear();
            _model.Actions.Set(PointerActionMask.Empty, Key);
        }

        /// <summary>Выставить флаг «курсор над UI» в модели.</summary>
        internal static void SetOverUI(bool overUI)
        {
            _model.IsOverUI.Set(overUI, Key);
        }

        /// <summary>
        /// Репорт импульса скролла (пикс/тик, X — горизонталь, Y — вертикаль, соглашение UGUI) в
        /// <c>ScrollDelta</c>. Пушится драйвером (<c>ScrollInputDriver</c>) на каждый тик колеса/клавиши
        /// направления; обработка импульса (raycast + scroll-событие) — на подписчиках <c>ScrollDelta.OnUpdate</c>.
        /// Нулевую дельту драйвер отбрасывает сам; здесь гарда нет, чтобы не прятать осознанный Set(0).
        /// Импульс, равный предыдущему (щелчки колеса подряд), публикуется принудительно — дедуп реактивного
        /// поля съел бы его.
        /// </summary>
        internal static void ReportScroll(Vector2 delta)
        {
            if (_model.ScrollDelta.Value == delta)
                _model.ScrollDelta.ForceUpdate();
            else
                _model.ScrollDelta.Set(delta, Key);
        }
    }
}