using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Vortex.Unity.UI.StateSwitcher.Items
{
    public class AnimatorStateSwitch : StateItem
    {
        [SerializeField] private Animator _animator;
        [SerializeField]
        [ValueDropdown("$GetAnimatorStatesKeys")]private string _stateName;
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
        

        public override string DropDownItemName => "AnimatorStateSwitch";
        public override string DropDownGroupName => "Graphics";
        public override StateItem Clone()
        {
            return new AnimatorStateSwitch()
            {
                _stateNumber = _stateNumber,
                _stateName = _stateName,
                _animator = _animator,
            };
        }
        
        private List<string> GetAnimatorStatesKeys()
        {
            var result = new List<string>();
            if (_animator != null)
                foreach (var param in _animator.parameters)
                    if (param.type == AnimatorControllerParameterType.Int)
                        result.Add(param.name);
            return result;
        }
#endif
    }
}