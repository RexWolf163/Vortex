using System.Collections.Generic;
using UnityEngine;
using Vortex.Core.Extensions.LogicExtensions;
using Vortex.Core.LocalizationSystem;
using Vortex.Core.VideoSystem;
using Vortex.Core.VideoSystem.Model;
using Vortex.Unity.UI.Misc.DropDown;

namespace Vortex.Unity.VideoSystem.Handlers
{
    public class ScreenModeHandler : MonoBehaviour
    {
        [SerializeField] private DropDownComponent dropDownComponent;

        [SerializeField] private string localeTagPrefix = "";

        [SerializeField] private ScreenMode[] whiteList = new ScreenMode[0];

        private List<string> _list = new();

        private List<string> _whiteList = new();

        private void Awake()
        {
            _whiteList.Clear();
            foreach (var mode in whiteList)
                _whiteList.Add(mode.ToString());
        }

        private void OnEnable()
        {
            var list = VideoController.GetScreenModes();
            _list.Clear();
            var ar = new List<string>();
            foreach (var mode in list)
            {
                if (!_whiteList.Contains(mode)) continue;
                ar.Add($"{localeTagPrefix}{mode}".Translate());
                _list.Add(mode);
            }

            var index = _list.IndexOfItem(VideoController.GetScreenMode());
            dropDownComponent.SetList(ar, SetScreenMode, index);
        }

        private void SetScreenMode(int index)
        {
            if (index < 0 || index >= _list.Count)
            {
                Debug.LogError($"[ScreenModeHandler] Invalid screen mode index selected: {index}");
                return;
            }

            VideoController.SetScreenMode(_list[index]);
        }
    }
}