using Sirenix.OdinInspector;
using UnityEngine;

namespace Vortex.Unity.UI.StateSwitcher.Handlers
{
    /// <summary>
    /// При активации выставляет StateSwitcher в указанное состояние
    /// </summary>
    public class OnEnableStateRunner : MonoBehaviour
    {
        [SerializeField] private UIStateSwitcher _stateSwitcher;

        [ShowIf("@_stateSwitcher != null")] [SerializeField] [ValueDropdown("@_stateSwitcher.GetDropDownStatesList()")]
        private int _stateToOpen;

        private void OnEnable()
        {
            _stateSwitcher.Set(_stateToOpen);
        }
    }
}