using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using Vortex.Unity.EditorTools.Attributes;
using Vortex.Unity.UI.VirtualCursorSystem.Controllers;
using Vortex.Unity.UI.VirtualCursorSystem.Model;

namespace Vortex.Unity.UI.VirtualCursorSystem.InputDrivers
{
    /// <summary>Число пальцев распознаваемого жеста.</summary>
    public enum GestureFingers
    {
        /// <summary>Два пальца.</summary>
        Two,

        /// <summary>Три пальца.</summary>
        Three,

        /// <summary>Четыре пальца.</summary>
        Four,

        /// <summary>Пять пальцев.</summary>
        Five,
    }

    /// <summary>Вид распознаваемого жеста.</summary>
    public enum GestureKind
    {
        /// <summary>
        /// Тап: все пальцы приложены и подняты быстрее порога времени и без заметного сдвига. Действие берётся и
        /// отпускается в одном кадре (клик).
        /// </summary>
        Tap,

        /// <summary>Удержание: действие держится, пока на экране ровно это число пальцев.</summary>
        Hold,
    }

    /// <summary>
    /// Драйвер жестов мультикасания. Два слоя:
    /// <list type="number">
    /// <item><b>Кадр жеста</b> — пока на экране два и больше пальцев, в <c>VirtualCursorBus.Gesture.Frame</c>
    /// публикуется <see cref="GestureFrame"/>: центр, сдвиг центра (панорамирование), размах и его отношение к прошлому
    /// кадру (расширение/сужение), поворот. Began — когда набралось число пальцев, Changed — кадры с изменением,
    /// Ended — пальцы подняты или их набор изменился (тогда сразу начинается новый жест — без скачка центра и размаха).
    /// Каждый канал (сдвиг, масштаб, поворот) молчит, пока его накопленное изменение с начала жеста не превысит порог,
    /// — дрожание пальцев не даёт микро-зума.</item>
    /// <item><b>Распознанные жесты → маска действий</b> — привязки «пальцы, вид (Tap/Hold), действие». Действие
    /// берётся от имени привязки (набор держателей), опционально курсор сначала переносится в центр жеста.</item>
    /// </list>
    /// Касания читаются через экшен, забинженный на <c>&lt;Touchscreen&gt;/touch*/position</c>: собираются его контролы
    /// по идущим касаниям. Выключенный экшен (карта не включена) — касаний нет, жест завершается.
    /// </summary>
    [Serializable]
    public class GestureInputDriver : InputDriver
    {
        /// <summary>Привязка распознанного жеста к действию маски.</summary>
        [Serializable]
        public struct Binding
        {
            [Tooltip("Число пальцев жеста.")]
            public GestureFingers fingers;

            [Tooltip("Tap — клик (взять и отпустить в одном кадре). Hold — держать, пока на экране это число пальцев.")]
            public GestureKind kind;

            [Tooltip("Действие маски Actions.")]
            public PointerAction action;

            [Tooltip("Перед взятием действия перенести курсор в центр жеста — клик придётся по месту касания.")]
            public bool moveCursorToCenter;
        }

        /// <summary>Минимальный размах/радиус (пиксели), при котором масштаб и поворот считаются.</summary>
        private const float MinSpread = 1f;

        [SerializeField, ValueSelector("GetInputActions"),
         Tooltip("Экшен позиций касаний — <Touchscreen>/touch*/position (Value/Vector2 или PassThrough).")]
        private string touchPositionActionId;

        [SerializeField, Tooltip("Платформы подключения драйвера (см. TouchPlatformFilter).")]
        private TouchPlatformFilter platformFilter = TouchPlatformFilter.All;

        [SerializeField, Tooltip("Скрывать курсор, когда привязка переносит его в центр жеста (прямой контакт).")]
        private bool hidesCursor = true;

        [SerializeField, Min(0f), Tooltip("Порог панорамирования: суммарный сдвиг центра с начала жеста, пиксели.")]
        private float panThreshold = 10f;

        [SerializeField, Min(0f), Tooltip("Порог масштаба: суммарное относительное изменение размаха с начала " +
                                          "жеста (0.05 = 5%).")]
        private float scaleThreshold = 0.05f;

        [SerializeField, Min(0f), Tooltip("Порог поворота: суммарный поворот с начала жеста, градусы.")]
        private float rotationThreshold = 5f;

        [SerializeField, Min(0f), Tooltip("Тап: максимальная длительность от первого касания до отрыва последнего " +
                                          "пальца, секунды.")]
        private float tapMaxDuration = 0.25f;

        [SerializeField, Min(0f), Tooltip("Тап: максимальный сдвиг любого пальца от точки его касания, пиксели.")]
        private float tapMaxMovement = 20f;

        /// <summary>Привязки распознанных жестов.</summary>
        [SerializeField] private Binding[] bindings = new Binding[0];

        /// <summary>Экшен позиций касаний (null — не разрезолвлен, драйвер бездействует).</summary>
        private InputAction _action;

        /// <summary>Позиции идущих касаний кадра по touchId.</summary>
        private readonly Dictionary<int, Vector2> _points = new();

        /// <summary>Позиции касаний прошлого кадра по touchId — для поворота.</summary>
        private readonly Dictionary<int, Vector2> _prevPoints = new();

        /// <summary>Набор пальцев (touchId) текущего жеста; пусто — жеста нет.</summary>
        private readonly HashSet<int> _gestureIds = new();

        /// <summary>Центр жеста прошлого кадра.</summary>
        private Vector2 _center;

        /// <summary>Размах жеста прошлого кадра.</summary>
        private float _spread;

        /// <summary>Накопленный с начала жеста сдвиг центра.</summary>
        private Vector2 _cumPan;

        /// <summary>Накопленный с начала жеста логарифм масштаба.</summary>
        private float _cumLogScale;

        /// <summary>Накопленный с начала жеста поворот, градусы.</summary>
        private float _cumRotation;

        /// <summary>Каналы, превысившие порог в текущем жесте.</summary>
        private bool _panOn, _scaleOn, _rotationOn;

        /// <summary>Идёт отслеживание тапа (с первого касания до отрыва последнего пальца).</summary>
        private bool _tapTracking;

        /// <summary>Время первого касания тапа (unscaled).</summary>
        private float _tapStart;

        /// <summary>Максимум одновременных пальцев за тап.</summary>
        private int _tapMaxFingers;

        /// <summary>Тап сорван (долго или палец сдвинулся).</summary>
        private bool _tapInvalid;

        /// <summary>Центр пальцев в момент максимума — точка тапа.</summary>
        private Vector2 _tapCenter;

        /// <summary>Точки касания пальцев тапа по touchId.</summary>
        private readonly Dictionary<int, Vector2> _tapStartPositions = new();

        /// <summary>Ключи удержания действий по привязкам.</summary>
        private object[] _sources = new object[0];

        /// <summary>Hold-привязка сейчас держит действие.</summary>
        private bool[] _holding = new bool[0];

        /// <summary>Ошибка типа контрола уже залогирована (анти-спам).</summary>
        private bool _typeErrorLogged;

        /// <summary>Касания опрашиваются каждый кадр.</summary>
        public override bool NeedsTick => true;

        /// <summary>Скрытие курсора по <see cref="hidesCursor"/>.</summary>
        public override bool HidesCursor => hidesCursor;

        /// <summary>Поддержка платформы по <see cref="platformFilter"/>.</summary>
        public override bool SupportsPlatform(RuntimePlatform platform) => platformFilter.Matches(platform);

        /// <summary>Резолв экшена касаний (ошибка настройки — LogError, драйвер бездействует) и ключей привязок.</summary>
        public override void Connect()
        {
            _action = string.IsNullOrEmpty(touchPositionActionId) ? null : ResolveAction(touchPositionActionId);
            if (_action == null)
                Debug.LogError("[GestureInputDriver] Ошибка настройки драйвера управления");

            _sources = new object[bindings.Length];
            for (var i = 0; i < _sources.Length; i++)
                _sources[i] = new object();
            _holding = new bool[bindings.Length];
        }

        /// <summary>Завершить идущий жест, отпустить удержания и сбросить состояние.</summary>
        public override void Disconnect()
        {
            if (_gestureIds.Count > 0)
                EndGesture();

            for (var i = 0; i < _holding.Length; i++)
                if (_holding[i])
                    VirtualCursorController.SetAction(bindings[i].action, false, _sources[i]);

            _action = null;
            _points.Clear();
            _prevPoints.Clear();
            _tapTracking = false;
            _tapStartPositions.Clear();
            _sources = new object[0];
            _holding = new bool[0];
        }

        /// <summary>Кадр: собрать касания → кадр жеста → тап → удержания.</summary>
        public override void Tick(float unscaledDeltaTime)
        {
            CollectPoints();
            UpdateGesture();
            UpdateTap();
            UpdateHolds();

            _prevPoints.Clear();
            foreach (var p in _points)
                _prevPoints[p.Key] = p.Value;
        }

        /// <summary>Позиции идущих касаний из контролов экшена (по touchId — дубли primaryTouch схлопываются).</summary>
        private void CollectPoints()
        {
            _points.Clear();
            if (_action == null || !_action.enabled)
            {
                // Карта выключена — касаний нет: жест и удержания завершатся штатно, а незаконченный тап
                // сбрасывается — исчезновение пальцев здесь не их отрыв, ложного тапа быть не должно.
                _tapTracking = false;
                return;
            }

            foreach (var control in _action.controls)
            {
                var touch = FindTouch(control);
                if (touch == null || !touch.isInProgress) continue;

                if (control is InputControl<Vector2> position)
                {
                    _points[touch.touchId.ReadValue()] = position.ReadValue();
                }
                else if (!_typeErrorLogged)
                {
                    Debug.LogError($"[GestureInputDriver] Экшен '{touchPositionActionId}' забинжен не на позицию " +
                                   "касания. Нужен <Touchscreen>/touch*/position.");
                    _typeErrorLogged = true;
                }
            }
        }

        /// <summary>Слой 1: начало, изменение и конец жеста по набору пальцев.</summary>
        private void UpdateGesture()
        {
            var active = _gestureIds.Count > 0;
            if (_points.Count < 2)
            {
                if (active) EndGesture();
                return;
            }

            Measure(out var center, out var spread);

            if (!active || !SameFingers())
            {
                if (active) EndGesture();
                BeginGesture(center, spread);
                return;
            }

            var pan = center - _center;
            var scale = _spread > MinSpread && spread > MinSpread ? spread / _spread : 1f;
            var rotation = MeasureRotation(center);
            _center = center;
            _spread = spread;

            _cumPan += pan;
            _cumLogScale += Mathf.Log(scale);
            _cumRotation += rotation;
            _panOn |= _cumPan.magnitude >= panThreshold;
            _scaleOn |= Mathf.Abs(_cumLogScale) >= Mathf.Log(1f + scaleThreshold);
            _rotationOn |= Mathf.Abs(_cumRotation) >= rotationThreshold;

            var outPan = _panOn ? pan : Vector2.zero;
            var outScale = _scaleOn ? scale : 1f;
            var outRotation = _rotationOn ? rotation : 0f;
            if (outPan == Vector2.zero && Mathf.Approximately(outScale, 1f) && Mathf.Approximately(outRotation, 0f))
                return;

            VirtualCursorController.ReportGesture(new GestureFrame(_gestureIds.Count, GesturePhase.Changed, center,
                outPan, spread, outScale, outRotation));
        }

        /// <summary>Начать жест текущим набором пальцев: сброс накоплений, кадр Began.</summary>
        private void BeginGesture(Vector2 center, float spread)
        {
            _gestureIds.Clear();
            foreach (var id in _points.Keys)
                _gestureIds.Add(id);

            _center = center;
            _spread = spread;
            _cumPan = Vector2.zero;
            _cumLogScale = 0f;
            _cumRotation = 0f;
            _panOn = _scaleOn = _rotationOn = false;

            VirtualCursorController.ReportGesture(new GestureFrame(_gestureIds.Count, GesturePhase.Began, center,
                Vector2.zero, spread, 1f, 0f));
        }

        /// <summary>Закончить жест: кадр Ended с последними центром и размахом.</summary>
        private void EndGesture()
        {
            VirtualCursorController.ReportGesture(new GestureFrame(_gestureIds.Count, GesturePhase.Ended, _center,
                Vector2.zero, _spread, 1f, 0f));
            _gestureIds.Clear();
        }

        /// <summary>Набор пальцев кадра совпадает с набором жеста.</summary>
        private bool SameFingers()
        {
            if (_points.Count != _gestureIds.Count) return false;
            foreach (var id in _points.Keys)
                if (!_gestureIds.Contains(id))
                    return false;
            return true;
        }

        /// <summary>Центр пальцев кадра и средний размах (расстояние до центра).</summary>
        private void Measure(out Vector2 center, out float spread)
        {
            center = Vector2.zero;
            foreach (var p in _points.Values)
                center += p;
            center /= _points.Count;

            spread = 0f;
            foreach (var p in _points.Values)
                spread += (p - center).magnitude;
            spread /= _points.Count;
        }

        /// <summary>
        /// Средний поворот пальцев вокруг центра относительно прошлого кадра, градусы (против часовой — положительный).
        /// Пальцы ближе <see cref="MinSpread"/> к центру не учитываются.
        /// </summary>
        private float MeasureRotation(Vector2 center)
        {
            var sum = 0f;
            var n = 0;
            foreach (var p in _points)
            {
                if (!_prevPoints.TryGetValue(p.Key, out var prev)) continue;
                var from = prev - _center;
                var to = p.Value - center;
                if (from.magnitude < MinSpread || to.magnitude < MinSpread) continue;
                sum += Vector2.SignedAngle(from, to);
                n++;
            }

            return n > 0 ? sum / n : 0f;
        }

        /// <summary>
        /// Слой 2, тап: от первого касания до отрыва последнего пальца — не дольше <see cref="tapMaxDuration"/> и ни один
        /// палец не сдвинулся дальше <see cref="tapMaxMovement"/>; число пальцев — максимум одновременных.
        /// </summary>
        private void UpdateTap()
        {
            var now = Time.unscaledTime;
            if (_points.Count > 0)
            {
                if (!_tapTracking)
                {
                    _tapTracking = true;
                    _tapStart = now;
                    _tapMaxFingers = 0;
                    _tapInvalid = false;
                    _tapStartPositions.Clear();
                }

                var maxSqr = tapMaxMovement * tapMaxMovement;
                foreach (var p in _points)
                {
                    if (!_tapStartPositions.TryGetValue(p.Key, out var start))
                        _tapStartPositions[p.Key] = p.Value;
                    else if ((p.Value - start).sqrMagnitude > maxSqr)
                        _tapInvalid = true;
                }

                if (_points.Count > _tapMaxFingers)
                {
                    _tapMaxFingers = _points.Count;
                    Measure(out _tapCenter, out _);
                }

                if (now - _tapStart > tapMaxDuration)
                    _tapInvalid = true;
                return;
            }

            if (!_tapTracking) return;
            _tapTracking = false;
            if (_tapInvalid || now - _tapStart > tapMaxDuration) return;

            for (var i = 0; i < bindings.Length; i++)
            {
                var b = bindings[i];
                if (b.kind != GestureKind.Tap || Count(b.fingers) != _tapMaxFingers) continue;

                if (b.moveCursorToCenter)
                    Report(_tapCenter, Touchscreen.current);
                ReportDevice(Touchscreen.current);
                VirtualCursorController.SetAction(b.action, true, _sources[i]);
                VirtualCursorController.SetAction(b.action, false, _sources[i]);
            }
        }

        /// <summary>Слой 2, удержание: действие держится, пока идёт жест ровно с числом пальцев привязки.</summary>
        private void UpdateHolds()
        {
            for (var i = 0; i < bindings.Length; i++)
            {
                var b = bindings[i];
                if (b.kind != GestureKind.Hold) continue;

                var hold = _gestureIds.Count == Count(b.fingers);
                if (hold == _holding[i]) continue;

                if (hold && b.moveCursorToCenter)
                    Report(_center, Touchscreen.current);
                if (hold)
                    ReportDevice(Touchscreen.current);
                _holding[i] = hold;
                VirtualCursorController.SetAction(b.action, hold, _sources[i]);
            }
        }

        /// <summary>Число пальцев из <see cref="GestureFingers"/>.</summary>
        private static int Count(GestureFingers fingers) => (int)fingers + 2;

        /// <summary>Касание, которому принадлежит контрол (сам контрол или предок); null — не касание.</summary>
        private static TouchControl FindTouch(InputControl control)
        {
            for (var c = control; c != null; c = c.parent)
                if (c is TouchControl touch)
                    return touch;
            return null;
        }
    }
}
