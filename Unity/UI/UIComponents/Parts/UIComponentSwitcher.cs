using Sirenix.OdinInspector;
using UnityEngine;
using Vortex.Unity.UI.StateSwitcher;

namespace Vortex.Unity.UI.UIComponents.Parts
{
    public class UIComponentSwitcher : UIComponentPart
    {
        [SerializeField] private UIStateSwitcher switcher;

#if UNITY_EDITOR
        [OnInspectorInit]
        private void Search()
        {
            if (switcher != null)
                return;
            switcher = GetComponent<UIStateSwitcher>();
        }
#endif
        public void PutData(int enumValue)
        {
            switcher.Set(enumValue);
        }

        public int GetValue() => switcher.State < 0 ? 0 : switcher.State;
    }
}