using UnityEngine;
using Vortex.Unity.AppSystem.System.TimeSystem;
using Vortex.Unity.UI.VirtualCursorSystem.Focus;

namespace Vortex.Unity.UI.VirtualCursorSystem.Handlers
{
    /// <summary>
    /// Перетягивание фокуса на связанный <see cref="FocusTargetComponent"/> при включении объекта.
    ///
    /// <b>Для чего.</b> Например, на объекте состояния «выбран» пункта выпадающего списка: при открытии
    /// списка фокус уходит на выбранный пункт, а не на ближайший к курсору (авто-передача фокуса группе);
    /// <see cref="FocusCenterScrollRect"/> на смене фокуса прокручивает к нему.
    ///
    /// <b>Только в режиме навигации.</b> Вне его фокус не ставится (<see cref="VirtualCursorFocusController.FocusTo"/>).
    ///
    /// <b>Почему через кадр.</b> На момент <c>OnEnable</c> цель может быть ещё не включена и не зарегистрирована
    /// в группе (порядок включения в иерархии); к отложенному вызову она готова.
    /// </summary>
    public class FocusDropDownSelected : MonoBehaviour
    {
        [SerializeField] private FocusTargetComponent target;

        private void OnEnable() => TimeController.Accumulate(PullFocus, this);

        private void OnDisable() => TimeController.RemoveCall(this);

        private void PullFocus() => VirtualCursorFocusController.FocusTo(target);
    }
}
