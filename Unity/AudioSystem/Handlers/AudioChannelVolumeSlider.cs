using UnityEngine;
using UnityEngine.UI;
using Vortex.Core.AudioSystem.Bus;
using Vortex.Core.AudioSystem.Model;
using Vortex.Unity.AudioSystem.Attributes;
using Vortex.Unity.EditorTools.Attributes;

namespace Vortex.Unity.AudioSystem.Handlers
{
    public class AudioChannelVolumeSlider : MonoBehaviour
    {
        [SerializeField, AutoLink] private Slider slider;

        [SerializeField, AudioChannelName] private string channel;

        private AudioChannel _audioChannel;

        private void Awake()
        {
            slider.maxValue = 1f;
        }

        private void OnEnable()
        {
            _audioChannel = AudioController.GetChannel(channel);
            if (_audioChannel == null)
            {
                slider.value = 0;
                return;
            }

            slider.value = _audioChannel.Volume;

            slider.onValueChanged.AddListener(OnChange);
        }

        private void OnChange(float value)
        {
            AudioController.SetChVolume(channel, value);
        }

        private void OnDisable()
        {
            slider.onValueChanged.RemoveListener(OnChange);
        }
    }
}