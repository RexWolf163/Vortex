using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Vortex.Unity.EditorTools.Attributes;

namespace Vortex.Unity.UI.VirtualCursorSystem
{
    /// <summary>
    /// Автоцентрирование контента <see cref="ScrollRect"/> на текущем фокусе. Вешается на
    /// GameObject со ScrollRect; подписывается на <c>VirtualCursorBus.Focus.CurrentFocus</c>
    /// и при смене фокуса, если новый target — потомок <see cref="ScrollRect.content"/>,
    /// скроллит контент так, чтобы точка target'а оказалась в центре <see cref="ScrollRect.viewport"/>.
    ///
    /// <b>Для чего.</b> D-pad навигация внутри списка: при переходе фокуса на элемент за
    /// пределами viewport'а список сам пролистывается, фокусный элемент виден. Без этого
    /// пользователь, нажав Down на последнем видимом элементе, не увидит, куда ушёл фокус.
    ///
    /// <b>Что если target в другом ScrollRect / не в этом content'е.</b> Молча пропускаем
    /// (<c>Transform.IsChildOf</c>-гард): компонент работает только по своему контексту,
    /// нескольких ScrollRect на одной сцене с собственными компонентами достаточно.
    ///
    /// <b>Горизонталь/вертикаль.</b> Независимые тоггл-поля + уважение <c>ScrollRect.horizontal</c>
    /// и <c>vertical</c>. Если ScrollRect только вертикальный — горизонтальное центрирование
    /// автоматически выключено, даже если <see cref="centerHorizontal"/>=true.
    ///
    /// <b>Анимация.</b> При <see cref="animationDuration"/>&gt;0 — плавная интерполяция в
    /// корутине на <c>unscaledTime</c> (работает на паузе). При 0 — мгновенный скачок.
    /// Повторная смена фокуса во время анимации останавливает текущую корутину и стартует новую.
    ///
    /// <b>Over-scroll.</b> Записываем в <c>content.anchoredPosition</c> напрямую; ScrollRect с
    /// <c>movementType=Clamped</c> или <c>Elastic</c> сам поправит выход за границы в своём
    /// LateUpdate (микро-коррекция, визуально незаметно). При <c>Unrestricted</c> клампа нет
    /// по дизайну.
    /// </summary>
    public class FocusCenterScrollRect : MonoBehaviour
    {
        [SerializeField, Tooltip("Центрировать по горизонтали. Применяется только если ScrollRect.horizontal=true.")]
        private bool centerHorizontal = true;

        [SerializeField, Tooltip("Центрировать по вертикали. Применяется только если ScrollRect.vertical=true.")]
        private bool centerVertical = true;

        [SerializeField, Min(0f), Tooltip("Длительность анимации скролла (сек). 0 — мгновенный переход. " +
                                          "unscaledTime: работает даже на паузе (меню).")]
        private float animationDuration = 0.12f;

        [SerializeField, AutoLink] private ScrollRect _scrollRect;
        private FocusModel _focusModel;
        private Coroutine _animRoutine;

        private void OnEnable()
        {
            TrySubscribe();
            VirtualCursorBus.OnReady += TrySubscribe;
        }

        private void OnDisable()
        {
            VirtualCursorBus.OnReady -= TrySubscribe;
            if (_focusModel != null)
                _focusModel.CurrentFocus.OnUpdate -= OnFocusChanged;
            _focusModel = null;

            if (_animRoutine != null)
            {
                StopCoroutine(_animRoutine);
                _animRoutine = null;
            }
        }

        /// <summary>
        /// Подписка на реактив фокуса. Безопасна при повторных вызовах и при отсутствии модели:
        /// при initial Enable до инициализации <see cref="VirtualCursorFocusController"/>
        /// подписка состоится позже, когда <see cref="VirtualCursorBus.OnReady"/> выстрелит.
        /// </summary>
        private void TrySubscribe()
        {
            if (_focusModel != null) return;
            var focus = VirtualCursorBus.Focus;
            if (focus == null) return;
            _focusModel = focus;
            _focusModel.CurrentFocus.OnUpdate += OnFocusChanged;
        }

        private void OnFocusChanged(IFocusTarget target)
        {
            if (target == null) return;
            // IFocusTarget не обязан быть Component, но на практике единственный известный
            // имплементатор — FocusTargetComponent, который Component. Если нет — просто пропускаем,
            // центрировать всё равно нечего (нет Transform'а).
            if (target is not Component c || c == null) return;
            if (_scrollRect == null || _scrollRect.content == null || _scrollRect.viewport == null) return;

            var targetTransform = c.transform;
            // Гард контекста: центрируем только свой контент. target может быть в другом ScrollRect
            // или вообще вне иерархии — молча пропускаем. IsChildOf(self)=true, но content ≠ self,
            // так что false-positive на свой корень невозможен.
            if (!targetTransform.IsChildOf(_scrollRect.content)) return;

            CenterOn(targetTransform);
        }

        private void CenterOn(Transform target)
        {
            // ForceUpdateCanvases — если RectTransform target'а/контента только что перестроился
            // (напр. layout group пересчитал), без форс-апдейта InverseTransformPoint даст старую
            // позицию.
            Canvas.ForceUpdateCanvases();

            var viewport = _scrollRect.viewport;
            var content = _scrollRect.content;

            // Переводим target в локальные координаты viewport'а, считаем смещение до центра viewport'а.
            // Смещение применяем к anchoredPosition content'а — это именно то, насколько надо подвинуть
            // контент, чтобы target сместился на центр viewport'а.
            var viewportCenterLocal = viewport.rect.center;
            var targetInViewport = (Vector2)viewport.InverseTransformPoint(target.position);
            var delta = viewportCenterLocal - targetInViewport;

            var newAnchored = content.anchoredPosition + delta;

            // Уважаем оси ScrollRect: горизонталь двигаем, только если ScrollRect разрешает И тоггл on.
            if (!centerHorizontal || !_scrollRect.horizontal) newAnchored.x = content.anchoredPosition.x;
            if (!centerVertical || !_scrollRect.vertical) newAnchored.y = content.anchoredPosition.y;

            // Если целевая позиция уже выставлена (нулевой delta) — не стартуем корутину впустую.
            if ((newAnchored - content.anchoredPosition).sqrMagnitude < 0.01f) return;

            if (animationDuration <= 0f)
            {
                content.anchoredPosition = newAnchored;
                return;
            }

            if (_animRoutine != null) StopCoroutine(_animRoutine);
            _animRoutine = StartCoroutine(AnimateTo(newAnchored));
        }

        private IEnumerator AnimateTo(Vector2 target)
        {
            var start = _scrollRect.content.anchoredPosition;
            var elapsed = 0f;
            while (elapsed < animationDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / animationDuration);
                // Smoothstep easing — плавный въезд/выезд, без линейного «робо-скролла».
                var eased = t * t * (3f - 2f * t);
                _scrollRect.content.anchoredPosition = Vector2.LerpUnclamped(start, target, eased);
                yield return null;
            }

            _scrollRect.content.anchoredPosition = target;
            _animRoutine = null;
        }
    }
}