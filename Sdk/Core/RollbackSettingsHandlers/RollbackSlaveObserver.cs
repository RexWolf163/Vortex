using UnityEngine;
using UnityEngine.UI;
using Vortex.Unity.AppSystem.System.TimeSystem;
using Vortex.Unity.EditorTools.Attributes;
using Vortex.Unity.UI.Misc.DropDown;

namespace Vortex.Sdk.Core.RollbackSettingsHandlers
{
    /// <summary>
    /// Маркер поля настроек под откат
    /// Сделано отдельным файлом, так как нельзя передавать линк на неициированные элементы
    /// Так что нужен элемент-хэндлер на том же слое что и контролируемый элемент
    /// </summary>
    public class RollbackSlaveObserver : MonoBehaviour
    {
        [SerializeField] private RollbackSettings master;

        [SerializeField, AutoLink] private DropDownComponent dropdown;
        [SerializeField, AutoLink] private Slider slider;

        private void OnEnable()
        {
            TimeController.Call(Link, 0, this);
        }

        private void OnDestroy()
        {
            TimeController.RemoveCall(this);
        }

        private void Link()
        {
            if (dropdown != null)
                master.Link(dropdown);
            if (slider != null)
                master.Link(slider);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (master != null) return;
            master = transform.GetComponentInParent<RollbackSettings>(true);
        }
#endif
    }
}