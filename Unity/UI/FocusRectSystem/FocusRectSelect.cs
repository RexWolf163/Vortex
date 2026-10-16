using System;
using UnityEngine;
using UnityEngine.Events;
using Vortex.Unity.UI.StateSwitcher;
using Vortex.Unity.UI.TweenerSystem;

namespace Vortex.Unity.UI.FocusRectSystem
{
    /// <summary>
    /// Элемент состояния: при входе в состояние — фокус View на FocusRect,
    /// при выходе — возврат View в исходную позу по пресету View.
    /// Переключение A→B схлопывается в фокус B: оба запроса попадают в одну волну с ключом View
    /// </summary>
    [Serializable]
    public class FocusRectSelect : StateItem
    {
        [SerializeField] private FocusRectView view;

        [SerializeField] private RectTransform focusRect;

        [SerializeField] private FitRule rule;

        /// <summary>
        /// Пресет перехода фокуса. Не задан — фокус без твина
        /// </summary>
        [SerializeField] private TweenPreset preset;

        /// <summary>
        /// View пришёл в позу фокуса (по концу перехода). Перебитый переход — не вызывается
        /// </summary>
        [SerializeField] private UnityEvent onComplete = new();

        public override void Set()
        {
            if (view == null || focusRect == null) return;
            view.Focus(focusRect, rule, preset, onComplete: onComplete.Invoke);
        }

        public override void DefaultState()
        {
            if (view == null || focusRect == null) return;
            view.ResetPose();
        }

#if UNITY_EDITOR
        public override bool IsValid() => view != null && focusRect != null;
        public override string DropDownItemName => "FocusRectSelect";
        public override string DropDownGroupName => "Objects";

        public override StateItem Clone()
        {
            return new FocusRectSelect
            {
                view = view,
                focusRect = focusRect,
                rule = rule,
                preset = preset,
                onComplete = onComplete
            };
        }
#endif
    }
}
