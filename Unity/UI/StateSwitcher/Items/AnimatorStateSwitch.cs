using System.Linq;
using Sirenix.OdinInspector;
using UnityEngine;
#if UNITY_EDITOR
using Vortex.Unity.Extensions.Editor.Misc;
#endif

namespace Vortex.Unity.UI.StateSwitcher.Items
{
    public class AnimatorStateSwitch : StateItem
    {
        [SerializeField] private Animator _animator;

        [SerializeField] [ValueDropdown("$GetAnimatorStatesKeys")]
        private string _stateName;

        [SerializeField] private int _stateNumber;
        [SerializeField] private int _defaultStateNumber = 0;

        public override void Set()
        {
            _animator.SetInteger(_stateName, _stateNumber);
        }

        public override void DefaultState()
        {
            _animator.SetInteger(_stateName, _defaultStateNumber);
        }

#if UNITY_EDITOR

        public override bool IsValid() => _animator != null && GetAnimatorStatesKeys().Contains(_stateName);

        public override string DropDownItemName => "AnimatorStateSwitch";
        public override string DropDownGroupName => "Animator Control";

        public override StateItem Clone()
        {
            return new AnimatorStateSwitch()
            {
                _stateNumber = _stateNumber,
                _stateName = _stateName,
                _animator = _animator,
            };
        }

        private string[] GetAnimatorStatesKeys()
        {
            if (_animator == null) return new string[0];
            return _animator.GetAnimatorParameters<int>();
        }
#endif
    }
}