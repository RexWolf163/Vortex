using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Events;

namespace Vortex.Unity.UI.VirtualCursorSystem
{
    /// <summary>
    /// Регистрирует себя как цель фокус-навигации виртуального курсора в ближайшей
    /// родительской <see cref="FocusGroup"/>. На <c>OnEnable</c> — <c>Register</c> в группе,
    /// на <c>OnDisable</c> — <c>Unregister</c>. <see cref="FocusGroup"/>-ссылка кэшируется
    /// на первом Enable (lazy): <c>GetComponentInParent</c> дорогой, повторять не нужно.
    ///
    /// <b>Fail-loud при отсутствии группы.</b> Если в родительской иерархии нет
    /// <see cref="FocusGroup"/> — <c>Debug.LogError</c> и target в систему не регистрируется.
    /// Это ошибка настройки сцены (архитектурно: любой фокусируемый элемент должен
    /// принадлежать какому-то контексту навигации).
    ///
    /// <see cref="ScreenPoint"/> пересчитывается при каждом запросе — UGUI через
    /// <see cref="RectTransformUtility"/> (учитывает render mode канваса), world через
    /// активную <see cref="Camera"/> из LIFO-реестра <c>VirtualCursorController</c>.
    ///
    /// На нажатие направления контроллер зовёт <see cref="IFocusTarget.NotifyFocused"/>/
    /// <see cref="IFocusTarget.NotifyUnfocused"/>, которые проксируют в UnityEvent'ы
    /// (<see cref="onFocused"/>/<see cref="onUnfocused"/>) — настраиваемая реакция в
    /// Inspector без кода (подсветка, SFX, анимация).
    /// </summary>
    public class FocusTargetComponent : MonoBehaviour, IFocusTarget
    {
        /// <summary>Тип цели: UGUI (RectTransform на канвасе) или world (Transform в сцене).</summary>
        public enum TargetKind
        {
            UGUI,
            World
        }

        [SerializeField, Tooltip("UGUI (точка — центр RectTransform на канвасе) или World " +
                                 "(точка — Transform в сцене, проектируется камерой).")]
        private TargetKind kind = TargetKind.UGUI;

        [SerializeField, ShowIf(nameof(kind), TargetKind.UGUI),
         Tooltip("Для UGUI: RectTransform цели (центр считается в экранные координаты " +
                 "по render mode канваса).")]
        private RectTransform rectTarget;

        [SerializeField, ShowIf(nameof(kind), TargetKind.World),
         Tooltip("Для World: Transform цели (проектируется активной камерой из CameraProvider).")]
        private Transform worldTarget;

        [Tooltip("Вызывается, когда фокус пришёл на эту цель.")]
        public UnityEvent onFocused;

        [Tooltip("Вызывается, когда фокус ушёл с этой цели (на другую или ClearFocus).")]
        public UnityEvent onUnfocused;

        // Канвас кэшируем один раз — RectTransform.GetComponentInParent на каждый ScreenPoint
        // был бы заметно дорогим (может вызываться N раз за Navigate по всем targets).
        // Если канвас пересобирается / target перевешивается — соответствующие OnEnable/OnDisable
        // перерегистрируют компонент, и Awake вычислит кэш заново.
        private Canvas _canvas;

        // FocusGroup резолвится lazy на первом OnEnable и кэшируется навсегда (пока жив
        // компонент). _groupResolved различает «ещё не искали» и «искали, но не нашли» —
        // чтобы LogError стрельнул ровно один раз, а не на каждой активации.
        private FocusGroup _group;
        private bool _groupResolved;

        public Vector2 ScreenPoint
        {
            get
            {
                switch (kind)
                {
                    case TargetKind.UGUI:
                        if (rectTarget == null) return default;
                        // Для Overlay-канваса cam=null даёт прямые screen-координаты;
                        // для ScreenSpaceCamera/WorldSpace — worldCamera канваса.
                        var uiCam = (_canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                            ? _canvas.worldCamera
                            : null;
                        return RectTransformUtility.WorldToScreenPoint(uiCam, rectTarget.position);

                    case TargetKind.World:
                        if (worldTarget == null) return default;
                        var cam = VirtualCursorController.ActiveCamera;
                        return cam != null
                            ? (Vector2)cam.WorldToScreenPoint(worldTarget.position)
                            : default;

                    default:
                        return default;
                }
            }
        }

        public bool IsActive
        {
            get
            {
                if (!isActiveAndEnabled) return false;
                switch (kind)
                {
                    case TargetKind.UGUI: return rectTarget != null;
                    case TargetKind.World: return worldTarget != null && VirtualCursorController.ActiveCamera != null;
                    default: return false;
                }
            }
        }

        public void NotifyFocused() => onFocused?.Invoke();
        public void NotifyUnfocused() => onUnfocused?.Invoke();

        private void Awake()
        {
            if (kind == TargetKind.UGUI && rectTarget != null)
                _canvas = rectTarget.GetComponentInParent<Canvas>();
        }

        private void OnEnable()
        {
            if (!_groupResolved)
            {
                // includeInactive: true — родительская группа может быть ещё не активна
                // (например, в этом же кадре загружается сцена и порядок OnEnable не
                // гарантирован). Нам важна сама иерархическая принадлежность.
                _group = GetComponentInParent<FocusGroup>(includeInactive: true);
                _groupResolved = true;
                if (_group == null)
                {
                    Debug.LogError(
                        "[FocusTarget] Нет FocusGroup в родительской иерархии. " +
                        "Этот target не будет участвовать в фокус-навигации. " +
                        "Добавь FocusGroup на любого предка этого объекта.",
                        this);
                }
            }
            _group?.Register(this);
        }

        private void OnDisable() => _group?.Unregister(this);
    }
}
