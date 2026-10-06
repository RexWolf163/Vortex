using System.Collections.Generic;
using UnityEngine;

namespace Vortex.Unity.UI.VirtualCursorSystem
{
    /// <summary>
    /// Контейнер фокус-навигации для набора <see cref="IFocusTarget"/>-ей, живущих в его
    /// иерархии. MonoBehaviour. На <c>OnEnable</c> — push в LIFO-стек
    /// <see cref="VirtualCursorFocusController"/> (становится активной группой);
    /// на <c>OnDisable</c> — pop из стека. На <c>OnDestroy</c> — финальный cleanup
    /// (на случай destroy активного компонента без вызова OnDisable).
    ///
    /// <b>Регистрация target'ов.</b> <see cref="FocusTargetComponent"/>.OnEnable ищет
    /// ближайшую родительскую <c>FocusGroup</c> через <c>GetComponentInParent</c> и зовёт
    /// её <see cref="Register"/>; на OnDisable — <see cref="Unregister"/>. Target без
    /// родительской группы — ошибка настройки сцены, компонент это фиксирует (fail-loud).
    ///
    /// <b>RememberedFocus.</b> Группа помнит свой последний <see cref="CurrentFocus"/>
    /// между активациями: закрыл меню паузы → открыл HUD → снова открыл меню → фокус
    /// возвращается туда, где был (при условии <c>_focusAnchor != null</c> у контроллера —
    /// т.е. пользователь остался в «nav mode», не двигал курсор мышью).
    ///
    /// <b>Nested groups.</b> Вложенные <c>FocusGroup</c> разрешены — target'ы цепляются
    /// к ближайшей родительской. LIFO обычно: push родителя → push вложенной → pop
    /// вложенной → снова активна родительская.
    /// </summary>
    public class FocusGroup : MonoBehaviour
    {
        [SerializeField, Tooltip("Исключить группу из поиска активной. При Ignore=true группа " +
                                 "остаётся зарегистрированной (target'ы в ней живут, RememberedFocus " +
                                 "сохраняется), но ActiveGroup её пропускает — Navigate её Targets " +
                                 "не видит. Используй для временного «паркинга» контекста (модалка " +
                                 "на экране, но навигация её не трогает). Проверяется live-геттером " +
                                 "ActiveGroup — тоггл в рантайме применяется со следующего Navigate.")]
        private bool ignore;

        [SerializeField, Range(0, 10),
         Tooltip("Приоритет группы (0..10). На PushGroup группа безусловно смещается к ВЕРХУ " +
                 "стека, минуя группы с СТРОГО МЕНЬШИМ приоритетом, и останавливается перед первой " +
                 "равной или большей (пузырёк). Между равными приоритетами действует обычный LIFO " +
                 "(позже push'нутый — выше). Пример: HUD(0) + Pause(5) + Toast(3) → порядок " +
                 "[HUD, Toast, Pause]; активной остаётся Pause. 0 = обычный LIFO без «подпора».")]
        private int priority;

        private readonly List<IFocusTarget> _targets = new();

        /// <summary>Target'ы этой группы (read-only). Порядок — по регистрации (OnEnable).</summary>
        public IReadOnlyList<IFocusTarget> Targets => _targets;

        /// <summary>
        /// Исключение из поиска активной группы. Live-свойство: тоггл в рантайме применяется
        /// немедленно — setter уведомляет <see cref="VirtualCursorFocusController"/>, который
        /// при смене <c>ActiveGroup</c> (например, из-за Ignore ЭТОЙ группы или наоборот —
        /// возврата её в игру) корректно переносит фокус (NotifyUnfocused на старом + auto-focus
        /// на новом в nav-mode). Группа остаётся в стеке — target'ы не теряются, RememberedFocus
        /// не сбрасывается. Setter idempotent: присвоение того же значения — no-op.
        /// </summary>
        public bool Ignore
        {
            get => ignore;
            set
            {
                if (ignore == value) return;
                ignore = value;
                // Передаём себя в контроллер — он решит, нужен ли перенос фокуса
                // (сравнит, в активной группе ли current). Проверка на initialized и null
                // внутри контроллера.
                VirtualCursorFocusController.OnGroupIgnoreChanged(this);
            }
        }

        /// <summary>
        /// Приоритет (0..10). Определяет позицию группы в стеке на <c>PushGroup</c>: группа
        /// бабблится к топу, минуя все с СТРОГО меньшим приоритетом. Изменение в рантайме НЕ
        /// переупорядочивает уже-зарегистрированные группы — применяется только на следующий
        /// push этой группы (OnDisable → OnEnable переподнимет её с новым приоритетом).
        /// </summary>
        public int Priority => priority;

        /// <summary>
        /// Последний <c>CurrentFocus</c> этой группы — сохраняется между активациями
        /// для авто-передачи фокуса на возврате. Обнуляется только явно (через
        /// <see cref="ClearRememberedFocus"/>) или при <c>OnDestroy</c>.
        /// </summary>
        public IFocusTarget RememberedFocus { get; internal set; }

        private void OnEnable() => VirtualCursorFocusController.PushGroup(this);

        private void OnDisable() => VirtualCursorFocusController.RemoveGroup(this);

        private void OnDestroy()
        {
            // На случай если RemoveGroup уже прошёл на OnDisable — RemoveGroup idempotent.
            VirtualCursorFocusController.RemoveGroup(this);
            _targets.Clear();
            RememberedFocus = null;
        }

        /// <summary>
        /// Регистрация target'а в группе. Зовётся <see cref="FocusTargetComponent"/>.OnEnable.
        /// Idempotent: повторная регистрация без эффекта.
        /// </summary>
        public void Register(IFocusTarget target)
        {
            if (target == null || _targets.Contains(target)) return;
            _targets.Add(target);
        }

        /// <summary>
        /// Снятие target'а. Если он был <see cref="RememberedFocus"/> — обнуляем
        /// запомненный (чтобы по возврату не ссылаться на уничтоженный target).
        /// </summary>
        public void Unregister(IFocusTarget target)
        {
            if (target == null) return;
            _targets.Remove(target);
            if (ReferenceEquals(RememberedFocus, target))
                RememberedFocus = null;
            // Если этот target был текущим фокусом — контроллер снимет висящую ссылку
            // (модель не должна указывать на выбывший target до следующего Navigate).
            VirtualCursorFocusController.OnTargetUnregistered(target);
        }

        /// <summary>Явный сброс запомненного фокуса (например, по дизайн-решению «начинаем с нуля»).</summary>
        public void ClearRememberedFocus() => RememberedFocus = null;

        /// <summary>
        /// Ближайший активный target по чистой евклидовой дистанции от позиции курсора.
        /// Используется для авто-передачи фокуса на push/pop группы: там направления нет,
        /// просто берём «тот, что ближе». <c>null</c>, если активных target'ов нет.
        /// </summary>
        internal IFocusTarget FindNearestEuclidean(Vector2 cursorPos)
        {
            IFocusTarget best = null;
            var bestDistSqr = float.MaxValue;
            for (var i = 0; i < _targets.Count; i++)
            {
                var t = _targets[i];
                if (t == null || !t.IsActive) continue;
                var distSqr = (t.ScreenPoint - cursorPos).sqrMagnitude;
                if (distSqr < bestDistSqr)
                {
                    bestDistSqr = distSqr;
                    best = t;
                }
            }
            return best;
        }
    }
}
