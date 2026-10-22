using System;
using Sirenix.OdinInspector;
using UnityEngine;
using Vortex.Unity.AppSystem.System.TimeSystem;
using Vortex.Unity.EditorTools.Attributes;
using Vortex.Unity.UI.TweenerSystem;
using Vortex.Unity.UI.TweenerSystem.UniTaskTweener;

namespace Vortex.Unity.UI.FocusRectSystem
{
    /// <summary>
    /// Управляемый объект: приводится к фокусу на фрагменте пространства своего родителя
    /// (фрагмент размечается FocusRect) и возвращается в исходную позу.
    ///
    /// Исходная поза — растяжение по родителю: якоря 0..1, отступы 0, масштаб 1, поворот 0.
    /// View помнит текущую цель (фокус или исходная поза) и при включении мгновенно приходит в неё.
    /// Плавные запросы откладываются на конец кадра с ключом View — последний запрос в кадре побеждает.
    /// Мгновенные выполняются сразу и снимают отложенный запрос. Вне Play Mode все запросы мгновенные (с Undo).
    /// Колбэк завершения фокуса вызывается, когда View пришёл в позу: по концу твина, сразу при переходе без твина или
    /// мгновенном, при включении — для запроса к неактивному View. Перебитый запрос (новый запрос в том же кадре, твин
    /// прерван новым переходом) свой колбэк не вызывает.
    /// Плавный переход можно заморозить (<see cref="SetPause"/>) — View паузу не отслеживает, её ставят снаружи.
    /// Дети View раскладываются по стандартной логике Unity
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class FocusRectView : MonoBehaviour
    {
        [SerializeField, AutoLink] private RectTransform rect;

        [SerializeField] private FitMethod fitMethod = FitMethod.SizeDelta;

        /// <summary>
        /// Пресет плавного возврата в исходную позу. Не задан — возврат без твина
        /// </summary>
        [SerializeField] private TweenPreset returnPreset;

        /// <summary>
        /// Текущая цель — фокус. false — исходная поза
        /// </summary>
        private bool _hasFocus;

        private RectTransform _targetRect;
        private FitRule _targetRule;

        private readonly AsyncTween _tween = new();

        /// <summary>
        /// Колбэк завершения запроса к неактивному View — вызывается, когда цель применится в OnEnable
        /// </summary>
        private Action _pendingComplete;

        /// <summary>
        /// Фокус на фрагменте, размеченном <paramref name="focusRect"/> в пространстве родителя View
        /// </summary>
        /// <param name="focusRect">разметка фрагмента</param>
        /// <param name="rule">правило вписывания</param>
        /// <param name="preset">пресет перехода; не задан — переход без твина</param>
        /// <param name="instant">применить сразу, без твина и отложенной волны</param>
        /// <param name="onComplete">View пришёл в позу фокуса; перебитый запрос его не вызывает</param>
        public void Focus(RectTransform focusRect, FitRule rule, TweenPreset preset = null, bool instant = false,
            Action onComplete = null)
        {
            if (focusRect == null)
                throw new ArgumentNullException(nameof(focusRect), $"[FocusRectView] Не задан FocusRect у {name}");

            _hasFocus = true;
            _targetRect = focusRect;
            _targetRule = rule;
            Request(preset, instant, onComplete);
        }

        /// <summary>
        /// Заморозить / отпустить плавный переход: на паузе View стоит, после снятия переход продолжается с того же
        /// места. Флаг View: переход, начатый на паузе, стартует замороженным. Мгновенные запросы и приход в цель при
        /// включении паузу не ждут. Источник паузы — снаружи (например, пауза игры через хэндлер уровня Sdk)
        /// </summary>
        public void SetPause(bool paused) => _tween.SetPause(paused);

        /// <summary>
        /// Переход заморожен (<see cref="SetPause"/>)
        /// </summary>
        public bool IsPaused => _tween.IsPaused;

        /// <summary>
        /// Возврат в исходную позу по пресету возврата View
        /// </summary>
        /// <param name="instant">применить сразу, без твина и отложенной волны</param>
        public void ResetPose(bool instant = false)
        {
            _hasFocus = false;
            _targetRect = null;
            Request(returnPreset, instant, null);
        }

        private void Request(TweenPreset preset, bool instant, Action onComplete)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying) instant = true;
#endif
            // Новый запрос перебивает колбэк прошлого, ждавшего включения
            _pendingComplete = null;
            if (instant)
            {
                ApplyInstant();
                onComplete?.Invoke();
            }
            // Неактивный View только запоминает цель — она применится в OnEnable
            else if (isActiveAndEnabled)
                TimeController.Accumulate(() => RunTransition(preset, onComplete), this);
            else
                _pendingComplete = onComplete;
        }

        /// <summary>
        /// Кадр текущей цели. Цель-фокус с удалённым FocusRect — исключение
        /// </summary>
        private FocusFrame CalcTarget()
        {
            if (!_hasFocus)
                return FocusFrame.Identity;
            if (_targetRect == null)
                throw new InvalidOperationException($"[FocusRectView] FocusRect цели удалён у {name}");
            return FocusFrameCalculator.Calculate(rect, _targetRect, _targetRule);
        }

        private void ApplyInstant()
        {
            var target = CalcTarget();
            if (Application.isPlaying)
                TimeController.RemoveCall(this);
            _tween.Kill();
#if UNITY_EDITOR
            if (!Application.isPlaying)
                UnityEditor.Undo.RecordObject(rect, "Focus Rect");
#endif
            Apply(target);
        }

        private void RunTransition(TweenPreset preset, Action onComplete)
        {
            var target = CalcTarget();
            if (preset == null || preset.duration <= 0f)
            {
                _tween.Kill();
                Apply(target);
                onComplete?.Invoke();
                return;
            }

            var start = ReadPose();
            // Кривая пресета применяется здесь, а не через SetEase: твин на последнем шаге выставляет ровно 1,
            // а переход должен прийти к значению кривой в конце (пульсация возвращается к исходной позе)
            var curve = preset.curve;
            _tween.Set(() => 0f, t => Apply(FocusFrame.Lerp(start, target, curve != null ? curve.Evaluate(t) : t)), 1f,
                    preset.duration)
                .SetEase(EaseType.Linear)
                .SetToken(destroyCancellationToken)
                .OnComplete(onComplete)
                .Run();
        }

        /// <summary>
        /// Текущая поза View в терминах кадра. Коэффициент читается из обоих каналов —
        /// корректно и после смены <see cref="fitMethod"/>
        /// </summary>
        private FocusFrame ReadPose()
        {
            var parent = FocusFrameCalculator.GetParent(rect);
            var parentWidth = parent.rect.width;
            if (parentWidth <= 0f)
                throw new InvalidOperationException(
                    $"[FocusRectView] Нулевой размер родителя {parent.name} ({parent.rect.size}) у {name}");
            var k =rect.localScale.x * rect.rect.width / parentWidth;
            return new FocusFrame(rect.anchoredPosition, k, Mathf.DeltaAngle(0f, rect.localEulerAngles.z));
        }

        private void Apply(FocusFrame frame)
        {
            var parentSize = FocusFrameCalculator.GetParent(rect).rect.size;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.localRotation = Quaternion.Euler(0f, 0f, frame.Angle);
            if (fitMethod == FitMethod.Scale)
            {
                rect.sizeDelta = Vector2.zero;
                rect.localScale = new Vector3(frame.K, frame.K, 1f);
            }
            else
            {
                rect.localScale = Vector3.one;
                rect.sizeDelta = parentSize * (frame.K - 1f);
            }

            rect.anchoredPosition = frame.Offset;
        }

        private void OnEnable()
        {
            ApplyInstant();
            var complete = _pendingComplete;
            _pendingComplete = null;
            complete?.Invoke();
        }

        private void OnDisable() => TimeController.RemoveCall(this);

        private void OnDestroy()
        {
            TimeController.RemoveCall(this);
            _tween.Kill();
        }

        [Button("Reset")]
        private void ResetButton() => ResetPose(true);
    }
}
