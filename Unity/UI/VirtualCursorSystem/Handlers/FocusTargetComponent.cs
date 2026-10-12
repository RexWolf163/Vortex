using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Events;
using Vortex.Unity.EditorTools.Attributes;
using Vortex.Unity.UI.VirtualCursorSystem.Focus;

namespace Vortex.Unity.UI.VirtualCursorSystem.Handlers
{
    /// <summary>
    /// Регистрирует себя как цель фокус-навигации виртуального курсора в ближайшей
    /// родительской <see cref="FocusGroup"/>. На <c>OnEnable</c> — резолв группы +
    /// <c>Register</c>, на <c>OnDisable</c> — <c>Unregister</c> из последней известной
    /// группы. Резолв делается на КАЖДОМ OnEnable (а не только на первом), иначе
    /// переподвешивание target'а в другую иерархию (reparenting при UI-пулинге,
    /// динамическая компоновка меню, DontDestroyOnLoad move) оставляло бы его
    /// зарегистрированным в прежней группе → фантомная регистрация + отсутствие в
    /// новой. Стоимость <c>GetComponentInParent</c> на редких OnEnable незначима.
    ///
    /// <b>Fail-loud при отсутствии группы.</b> Если в родительской иерархии нет
    /// <see cref="FocusGroup"/> — <c>Debug.LogError</c> и target в систему не регистрируется.
    /// Это ошибка настройки сцены (архитектурно: любой фокусируемый элемент должен
    /// принадлежать какому-то контексту навигации). Лог стрельнёт один раз подряд —
    /// повторные OnEnable с теми же null-результатом молчат (анти-спам).
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
            /// <summary>Точка — центр <see cref="RectTransform"/> в экранных координатах (с учётом render mode канваса).</summary>
            UGUI,

            /// <summary>Точка — проекция <see cref="Transform"/> через активную камеру <see cref="CameraProvider"/>.</summary>
            World
        }

        [SerializeField, Tooltip("UGUI (точка — центр RectTransform на канвасе) или World " +
                                 "(точка — Transform в сцене, проектируется камерой).")]
        private TargetKind kind = TargetKind.UGUI;

        [SerializeField, ShowIf(nameof(kind), TargetKind.UGUI), AutoLink,
         Tooltip("Для UGUI: RectTransform цели (центр считается в экранные координаты " +
                 "по render mode канваса).")]
        private RectTransform rectTarget;

        [SerializeField, ShowIf(nameof(kind), TargetKind.World), AutoLink,
         Tooltip("Для World: Transform цели (проектируется активной камерой из CameraProvider).")]
        private Transform worldTarget;

        [Tooltip("Вызывается, когда фокус пришёл на эту цель.")]
        public UnityEvent onFocused;

        [Tooltip("Вызывается, когда фокус ушёл с этой цели (на другую или ClearFocus).")]
        public UnityEvent onUnfocused;

        /// <summary>
        /// Канвас UGUI-цели, кэш на OnEnable (как и <see cref="_group"/>): <c>GetComponentInParent</c> на каждый
        /// <see cref="ScreenPoint"/> был бы дорог — его зовут N раз за Navigate. Reparenting между
        /// OnDisable/OnEnable подхватывается ре-резолвом.
        /// </summary>
        private Canvas _canvas;

        /// <summary>
        /// Группа, в которой зарегистрирован target. Резолвится на каждом OnEnable (parent мог измениться:
        /// UI-пулинг, динамическая компоновка); OnDisable снимает из той же группы.
        /// </summary>
        private FocusGroup _group;

        /// <summary>
        /// Анти-спам LogError об отсутствии группы: взводится после лога, сбрасывается, когда группа найдена.
        /// </summary>
        private bool _missingGroupLogged;

        /// <inheritdoc/>
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
                        var camData = Controllers.VirtualCursorController.ActiveCamera;
                        var cam = camData.Cam;
                        return cam != null
                            ? (Vector2)cam.WorldToScreenPoint(worldTarget.position)
                            : default;

                    default:
                        return default;
                }
            }
        }

        /// <inheritdoc/>
        /// <remarks>Компонент активен и включён; для UGUI задан <see cref="rectTarget"/>, для World — <see cref="worldTarget"/> и есть активная камера.</remarks>
        public bool IsActive
        {
            get
            {
                if (!isActiveAndEnabled) return false;
                switch (kind)
                {
                    case TargetKind.UGUI: return rectTarget != null;
                    case TargetKind.World:
                        return worldTarget != null && Controllers.VirtualCursorController.ActiveCamera != null;
                    default: return false;
                }
            }
        }

        /// <inheritdoc/>
        /// <remarks>Вызывает <see cref="onFocused"/>.</remarks>
        public void NotifyFocused() => onFocused?.Invoke();

        /// <inheritdoc/>
        /// <remarks>Вызывает <see cref="onUnfocused"/>.</remarks>
        public void NotifyUnfocused() => onUnfocused?.Invoke();

        /// <inheritdoc/>
        /// <remarks>GameObject этого компонента.</remarks>
        public GameObject Target => gameObject;

        /// <summary>Найти ближайшую родительскую группу и зарегистрироваться в ней; без группы — LogError.</summary>
        private void OnEnable()
        {
            // Ре-резолв group и canvas на каждом OnEnable: parent мог измениться после
            // предыдущего OnDisable (reparenting, UI-пулинг, DontDestroyOnLoad move).
            // includeInactive: true — родительская группа может быть ещё не активна в этом
            // кадре (порядок OnEnable при загрузке сцены не гарантирован). Нам важна
            // сама иерархическая принадлежность, а не её текущее активное состояние.
            _group = GetComponentInParent<FocusGroup>(includeInactive: true);

            if (_group == null)
            {
                if (!_missingGroupLogged)
                {
                    Debug.LogError(
                        $"[FocusTarget] Нет FocusGroup в родительской иерархии {transform.parent?.name}.{transform.name}. " +
                        "Этот target не будет участвовать в фокус-навигации. " +
                        "Добавь FocusGroup на любого предка этого объекта.",
                        this);
                    _missingGroupLogged = true;
                }

                return;
            }

            _missingGroupLogged = false;

            if (kind == TargetKind.UGUI && rectTarget != null)
                _canvas = rectTarget.GetComponentInParent<Canvas>();

            _group.Register(this);
        }

        /// <summary>Сняться с группы, в которой регистрировались.</summary>
        private void OnDisable() => _group?.Unregister(this);
    }
}