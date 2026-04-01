using System;
using UnityEngine;
using Vortex.Core.AudioSystem;
using Vortex.Core.AudioSystem.Bus;
using Vortex.Core.Extensions.DefaultEnums;
using Vortex.Unity.UI.UIComponents;

namespace Vortex.Unity.AudioSystem.Handlers
{
    public class AudioSwitcher : MonoBehaviour
    {
        [SerializeField] private UIComponent uiComponent;
        [SerializeField] private SoundType controlType;

        private void OnEnable()
        {
            uiComponent.SetAction(OnChange);
            Refresh();
        }

        private void OnChange()
        {
            switch (controlType)
            {
                case SoundType.Master:
                    AudioController.SetMasterState(!AudioController.Settings.MasterOn);
                    break;
                case SoundType.Sound:
                    AudioController.SetSoundState(!AudioController.Settings.SoundOn);
                    break;
                case SoundType.Music:
                    AudioController.SetMusicState(!AudioController.Settings.MusicOn);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            Refresh();
        }

        private void OnDisable()
        {
        }

        private void Refresh()
        {
            var state = false;
            switch (controlType)
            {
                case SoundType.Master:
                    state = AudioController.Settings.MasterOn;
                    break;
                case SoundType.Sound:
                    state = AudioController.Settings.SoundOn;
                    break;
                case SoundType.Music:
                    state = AudioController.Settings.MusicOn;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            uiComponent.SetSwitcher(state ? SwitcherState.On : SwitcherState.Off);
        }
    }
}