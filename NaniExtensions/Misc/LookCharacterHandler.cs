using Naninovel;
using UnityEngine;
using Vortex.Unity.UI.Attributes;
using Vortex.Unity.UI.StateSwitcher;

namespace Vortex.NaniExtensions.Misc
{
    public class LookCharacterHandler : MonoBehaviour
    {
        [SerializeField, StateSwitcher(typeof(CharacterLookDirection))]
        private UIStateSwitcher switcher;

        public void SetLookCharacter(CharacterLookDirection lookDirection)
        {
            switcher.Set(lookDirection);
        }
    }
}