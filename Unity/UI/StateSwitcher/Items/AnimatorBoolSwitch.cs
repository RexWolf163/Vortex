using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Vortex.Unity.UI.StateSwitcher.Items
{
    [Serializable]
    public class AnimatorBoolSwitch : StateItem
    {
        [SerializeField] private Animator _animator;

        [SerializeField] [ValueDropdown("$GetAnimatorStatesKeys")]
        private string _boolParamName;

        public override void Set()
        {
            _animator.SetBool(_boolParamName, true);
        }

        public override void DefaultState()
        {
            _animator.SetBool(_boolParamName, false);
        }
#if UNITY_EDITOR


        public override string DropDownItemName => "AnimatorBoolSwitch";
        public override string DropDownGroupName => "Graphics";

        public override StateItem Clone()
        {
            return new AnimatorBoolSwitch()
            {
                _boolParamName = _boolParamName,
                _animator = _animator,
            };
        }

        private List<string> GetAnimatorStatesKeys()
        {
            var result = new List<string>();
            if (_animator != null)
                foreach (var param in _animator.parameters)
                    if (param.type == AnimatorControllerParameterType.Bool)
                        result.Add(param.name);
            return result;
        }
#endif
    }
}