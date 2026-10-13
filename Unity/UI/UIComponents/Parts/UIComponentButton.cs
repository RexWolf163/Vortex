using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Vortex.Unity.UI.Misc;

namespace Vortex.Unity.UI.UIComponents.Parts
{
    public class UIComponentButton : UIComponentPart
    {
        [SerializeField] private Button btn;
        [SerializeField] private AdvancedButton advBtn;

        private UnityAction _currentAction;

#if UNITY_EDITOR
        [OnInspectorInit]
        private void Search()
        {
            if (btn != null && advBtn != null)
                return;
            btn = GetComponent<Button>();
            advBtn = GetComponent<AdvancedButton>();
        }

        public override void SetDirty()
        {
            Dirty(btn);
            Dirty(advBtn);
        }
#endif
        public void PutData(UnityAction action)
        {
            if (_currentAction != null)
            {
                if (btn != null)
                    btn.onClick.RemoveListener(_currentAction);
                if (advBtn != null)
                    advBtn.RemoveOnClick(_currentAction);
            }

            _currentAction = action;
            if (action != null)
            {
                if (btn != null)
                    btn.onClick.AddListener(_currentAction);
                if (advBtn != null)
                    advBtn.AddOnClick(_currentAction);
            }
        }

        /// <summary>
        /// Программное нажатие кнопки части. <see cref="AdvancedButton"/> — его <c>Press()</c> (визуал и логика нажатия
        /// по режиму фиксации клика). <see cref="Button"/> своего API нажатия не имеет — ему отправляется PointerDown
        /// (визуальное состояние «нажата»).
        /// </summary>
        public void CallPress()
        {
            if (advBtn != null)
                advBtn.Press();
            if (btn != null)
                ExecuteEvents.Execute(btn.gameObject, new PointerEventData(EventSystem.current),
                    ExecuteEvents.pointerDownHandler);
        }

        /// <summary>
        /// Программное отпускание кнопки части. <see cref="AdvancedButton"/> — его <c>Release()</c> (клик по правилам
        /// режима фиксации; без предшествующего нажатия — ничего). <see cref="Button"/> — PointerUp и, если кнопка
        /// активна и интерактивна, <c>onClick</c>.
        /// </summary>
        public void CallRelease()
        {
            if (advBtn != null)
                advBtn.Release();
            if (btn != null)
            {
                ExecuteEvents.Execute(btn.gameObject, new PointerEventData(EventSystem.current),
                    ExecuteEvents.pointerUpHandler);
                if (btn.IsActive() && btn.IsInteractable())
                    btn.onClick.Invoke();
            }
        }

        private void OnDestroy()
        {
            if (_currentAction == null)
                return;
            if (btn != null)
                btn.onClick.RemoveListener(_currentAction);
            if (advBtn != null)
                advBtn.RemoveOnClick(_currentAction);
        }
    }
}