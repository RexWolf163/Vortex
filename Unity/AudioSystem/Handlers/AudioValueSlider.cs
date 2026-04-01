using System;
using UnityEngine;
using UnityEngine.UI;
using Vortex.Core.AudioSystem;
using Vortex.Core.AudioSystem.Bus;
using Vortex.Unity.EditorTools.Attributes;

namespace Vortex.Unity.AudioSystem.Handlers
{
    public class AudioValueSlider : MonoBehaviour
    {
        [SerializeField, AutoLink] private Slider slider;

        [SerializeField] private SoundType controlType;

        private void Awake()
        {
            slider.maxValue = 1f;
        }

        private void OnEnable()
        {
            switch (controlType)
            {
                case SoundType.Master:
                    slider.value = AudioController.Settings.MasterVolume;
                    break;
                case SoundType.Sound:
                    slider.value = AudioController.Settings.SoundVolume;
                    break;
                case SoundType.Music:
                    slider.value = AudioController.Settings.MusicVolume;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            slider.onValueChanged.AddListener(OnChange);
        }

        private void OnDisable()
        {
            slider.onValueChanged.RemoveListener(OnChange);
        }

        private void OnChange(float value)
        {
            switch (controlType)
            {
                case SoundType.Master:
                    AudioController.SetMasterVolume(value);
                    break;
                case SoundType.Sound:
                    AudioController.SetSoundVolume(value);
                    break;
                case SoundType.Music:
                    AudioController.SetMusicVolume(value);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }
}