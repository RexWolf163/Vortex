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
    /// <b>Ось-aware через порог <see cref="axisTolerance"/>.</b> На переходе фокуса считаем
    /// |delta.x| и |delta.y| в viewport-local координатах. Ось, по которой сдвиг МЕНЬШЕ
    /// <see cref="axisTolerance"/> — считается не изменившейся и исключается из центрирования.
    /// Диагональный переход (обе оси выше порога) центрирует обе; переход в пределах порога
    /// по обеим осям (мелкое смещение / тот же элемент) — не трогаем ничего (CenterOn отсечётся
    /// sqrMagnitude-гардом). Логика: при движении D-pad'ом Right в сеточной раскладке строки
    /// обычно идеально выровнены по Y, но +/−1 пиксель из-за пивотов не должен триггерить
    /// вертикальное «подпиливание». Для первого фокуса (нет prev) и при возврате фокуса после
    /// ClearFocus / ухода в чужой ScrollRect — центрируем обе оси (<see cref="_prevFocusTransform"/>=null).
    ///
    /// <b>Prev-tracker как Transform, не Vector2.</b> ScrollRect при прокрутке двигает дочерние
    /// RectTransform'ы контента физически (анимация <c>anchoredPosition</c>) без каких-либо
    /// событий. Если держать координату предыдущего фокуса кэшем — она устаревает в тот же кадр,
    /// как мы отскроллили. Ссылка на Transform — всегда live: <c>_prevFocusTransform.position</c>
    /// в OnFocusChanged даёт актуальную точку этого элемента в текущей позиции скролла.
    ///
    /// <b>Горизонталь/вертикаль-тоггл</b> поверх осиавойной логики: тоггл выключает ось
    /// полностью (например, тоггл vertical=false → никакое вертикальное движение не произойдёт
    /// независимо от delta). Уважение <c>ScrollRect.horizontal</c>/<c>vertical</c> — тот же гейт.
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

        [SerializeField, Min(0f),
         Tooltip("Порог движения по оси (пикс viewport-local), ниже которого ось считается НЕ " +
                 "изменившейся — по ней центрирование пропускается. Для сеточных раскладок: " +
                 "горизонтальный переход между двумя кнопками в одной строке даёт |Δy| ≈ 0 — порог " +
                 "защищает от микро-прыжков по вертикали из-за пивотных расхождений. 0 = любое " +
                 "движение по оси триггерит центрирование (эквивалент axis-aware выключен).")]
        private float axisTolerance = 10f;

        [SerializeField, AutoLink] private ScrollRect _scrollRect;
        private FocusModel _focusModel;
        private Coroutine _animRoutine;

        // Live-reference на Transform предыдущего фокусного элемента. Храним Transform,
        // а не Vector2-координату: ScrollRect прокручивает контент — все его дети физически
        // перемещаются без каких-либо событий; при следующем OnFocusChanged чтение
        // _prevFocusTransform.position возвращает актуальную точку этого элемента в
        // текущей позиции скролла. Null = нет истории (первый фокус / после ClearFocus /
        // после ухода фокуса в чужой ScrollRect) — центрируем обе оси.
        private Transform _prevFocusTransform;

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
            _prevFocusTransform = null;

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
            // ClearFocus / фокус пропал: сбрасываем prev-tracker, чтобы следующий фокус
            // обрабатывался как первый (центрируем обе оси). Иначе возврат фокуса давал бы
            // ось-aware решение против давно устаревшего эталона.
            if (target == null)
            {
                _prevFocusTransform = null;
                return;
            }

            // IFocusTarget не обязан быть Component, но на практике единственный известный
            // имплементатор — FocusTargetComponent, который Component. Если нет — просто пропускаем,
            // центрировать всё равно нечего (нет Transform'а).
            if (target is not Component c || c == null) return;
            if (_scrollRect == null || _scrollRect.content == null || _scrollRect.viewport == null) return;

            var targetTransform = c.transform;
            // Гард контекста: центрируем только свой контент. target может быть в другом ScrollRect
            // или вообще вне иерархии — молча пропускаем. При уходе фокуса из нашего content'а
            // ТАКЖЕ сбрасываем prev-tracker: следующий return фокуса в нашу зону должен
            // обрабатываться как первый, без оси-aware гадания об ушедшем-давно элементе.
            if (!targetTransform.IsChildOf(_scrollRect.content))
            {
                _prevFocusTransform = null;
                return;
            }

            // Определяем разрешённые оси для ЭТОГО центрирования. База — настроечные тоггл-ы
            // × возможности ScrollRect; затем сверху axis-aware гейт по доминирующей оси сдвига.
            var allowH = centerHorizontal && _scrollRect.horizontal;
            var allowV = centerVertical && _scrollRect.vertical;

            // Axis-aware через порог: если есть prev (не null и ещё жив как Unity-object),
            // выключаем ось, по которой сдвиг от prev к target МЕНЬШЕ axisTolerance. Prev мог быть
            // уже уничтожен (его FocusTarget OnDisable, GameObject destroyed, пулинг) — Unity
            // '!= null' ловит fake-null. При диагональном переходе, где обе оси превышают порог,
            // центрируем обе; при переходе, где обе под порогом (мелкое смещение / тот же элемент)
            // — обе оси отключены, но CenterOn всё равно отсечётся sqrMagnitude-гардом (delta ≈ 0).
            //
            // Порог считается в viewport-local пикселях, т.е. в тех же единицах, что viewport.rect.
            // Это согласовано с UGUI-пикселями на канвасе (RenderMode-независимо).
            if (_prevFocusTransform != null && _prevFocusTransform.IsChildOf(_scrollRect.content))
            {
                var viewport = _scrollRect.viewport;
                var prevLocal  = (Vector2)viewport.InverseTransformPoint(_prevFocusTransform.position);
                var newLocal   = (Vector2)viewport.InverseTransformPoint(targetTransform.position);
                var delta      = newLocal - prevLocal;

                if (Mathf.Abs(delta.x) < axisTolerance) allowH = false;
                if (Mathf.Abs(delta.y) < axisTolerance) allowV = false;
            }

            CenterOn(targetTransform, allowH, allowV);
            _prevFocusTransform = targetTransform;
        }

        private void CenterOn(Transform target, bool allowH, bool allowV)
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

            // Запрещённые оси: сохраняем текущее значение anchoredPosition по этой оси.
            // Разрешение формируется в OnFocusChanged: тоггл × ScrollRect.axis × axis-aware доминанта.
            if (!allowH) newAnchored.x = content.anchoredPosition.x;
            if (!allowV) newAnchored.y = content.anchoredPosition.y;

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