using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Events;
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