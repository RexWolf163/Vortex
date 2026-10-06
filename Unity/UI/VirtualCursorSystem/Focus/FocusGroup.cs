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
        private readonly List<IFocusTarget> _targets = new();

        /// <summary>Target'ы этой группы (read-only). Порядок — по регистрации (OnEnable).</summary>
        public IReadOnlyList<IFocusTarget> Targets => _targets;

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
