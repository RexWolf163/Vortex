using System;
using System.Linq;
using UnityEngine;
using Vortex.Core.VideoSystem;
using Vortex.Unity.UI.Misc.DropDown;

namespace Vortex.Unity.VideoSystem.Handlers
{
    public class ScreenResolutionHandler : MonoBehaviour
    {
        [SerializeField] private DropDownComponent dropDownComponent;

        private string[] _list;

        private void OnEnable()
        {
            _list = VideoController.GetResolutionsList().ToArray();

            var index = Array.IndexOf(_list, VideoController.GetResolution());
            dropDownComponent.SetList(_list, SetResolution, index);
        }

        private void SetResolution(int index)
        {
            if (index < 0 || index >= _list.Length)
            {
                Debug.LogError($"[ScreenModeHandler] Invalid screen resolution index selected: {index}");
                return;
            }

            VideoController.SetResolution(_list[index]);
        }
    }
}