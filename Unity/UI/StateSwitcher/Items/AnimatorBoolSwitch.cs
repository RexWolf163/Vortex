using System;
using System.Linq;
using Sirenix.OdinInspector;
using UnityEngine;
#if UNITY_EDITOR
using Vortex.Unity.Extensions.Editor.Misc;
#endif

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

        public override bool IsValid() => _animator != null && GetAnimatorStatesKeys().Contains(_boolParamName);

#if UNITY_EDITOR


        public override string DropDownItemName => "AnimatorBoolSwitch";
        public override string DropDownGroupName => "Animator Control";

        public override StateItem Clone()
        {
            return new AnimatorBoolSwitch()
            {
                _boolParamName = _boolParamName,
                _animator = _animator,
            };
        }

        private string[] GetAnimatorStatesKeys() => _animator.GetAnimatorParameters<bool>();
#endif
    }
}