using System;
using UnityEngine;
using Vortex.Unity.UI.TweenerSystem;
using Vortex.Unity.UI.VirtualCursorSystem.Bus;
using Vortex.Unity.UI.VirtualCursorSystem.Model;

namespace Vortex.Unity.UI.VirtualCursorSystem.Handlers
{
    /// <summary>
    /// Глобальная реакция твинеров на клавиши виртуального курсора: подписан на
    /// <c>VirtualCursorBus.Data.Actions</c> и для каждой привязки «действие → <see cref="TweenerHub"/>»
    /// на нажатие зовёт <see cref="TweenerHub.Forward"/>, на отпускание — <see cref="TweenerHub.Back"/>.
    /// Срабатывает независимо от того, над чем курсор (типичное место — префаб визуала курсора:
    /// анимация «нажатия» самого курсора). Для реакции конкретного UI-элемента — <see cref="PointerActionHandler"/>.
    ///
    /// На включении состояние твинеров мгновенно (skip) выравнивается по текущей маске: компонент,
    /// включённый при зажатой клавише, сразу показывает «нажато», выключенный — не залипает.
    ///
    /// Привязка к <see cref="PointerAction.None"/> никогда не активна (<see cref="PointerActionMask.IsActive"/>
    /// для None — false): её твинер всегда в состоянии Back.
    /// </summary>
    [AddComponentMenu("Vortex/Virtual Cursor/Pointer Action Tweener Handler")]
    public class PointerActionTweenerHandler : MonoBehaviour
    {
        /// <summary>Привязка «клавиша курсора → твинер».</summary>
        [Serializable]
        private struct Binding
        {
            [Tooltip("Клавиша курсора: BaseClick (LMB), ContextClick (RMB), AltClick (MMB).")]
            public PointerAction action;

            [Tooltip("Твинер: нажатие — Forward, отпускание — Back.")]
            public TweenerHub tweener;
        }

        [SerializeField, Tooltip("Привязки «клавиша курсора → твинер». На одну клавишу можно повесить несколько твинеров.")]
        private Binding[] bindings = new Binding[0];

        /// <summary>Маска действий на момент последней обработки — для выделения фронтов.</summary>
        private PointerActionMask _lastMask;

        /// <summary>Выровнять твинеры по текущей маске без анимации и подписаться на её изменения.</summary>
        private void OnEnable()
        {
            var actions = VirtualCursorBus.Data.Actions;
            _lastMask = actions.Value;
            foreach (var binding in bindings)
            {
                if (_lastMask.IsActive(binding.action)) binding.tweener.Forward(true);
                else binding.tweener.Back(true);
            }

            actions.OnUpdate += OnActionsChanged;
        }

        /// <summary>Отписка от изменений маски.</summary>
        private void OnDisable() => VirtualCursorBus.Data.Actions.OnUpdate -= OnActionsChanged;

        /// <summary>Только фронты: твинер дёргается, лишь когда состояние его клавиши реально сменилось.</summary>
        private void OnActionsChanged(PointerActionMask mask)
        {
            foreach (var binding in bindings)
            {
                var isActive = mask.IsActive(binding.action);
                if (isActive == _lastMask.IsActive(binding.action)) continue;

                if (isActive) binding.tweener.Forward();
                else binding.tweener.Back();
            }

            _lastMask = mask;
        }
    }
}
