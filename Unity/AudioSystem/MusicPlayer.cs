using Sirenix.OdinInspector;
using UnityEngine;
using Vortex.Core.AudioSystem.Bus;
using Vortex.Core.AudioSystem.Model;
using Vortex.Unity.AudioSystem.Model;

namespace Vortex.Unity.AudioSystem
{
    public class MusicPlayer : MonoBehaviour
    {
        [SerializeField] private AudioSource audioSource;

        private float _currentVolume;
        private float _currentPitch;

        private float _volumeMultiplier;

        [ShowInInspector, ReadOnly] private AudioChannel _channel;

        private void OnEnable()
        {
            if (audioSource == null)
            {
                Debug.LogError("[MusicPlayer] Audio Source is null");
                return;
            }

            _volumeMultiplier = 1;
            AudioController.OnSettingsChanged += CheckSettings;
            CheckSettings();
        }

        private void OnDisable()
        {
            if (audioSource == null)
                return;
            AudioController.OnSettingsChanged -= CheckSettings;
            Stop();
        }

        private void CheckSettings()
        {
            var b = audioSource.mute;
            audioSource.mute = !AudioController.GetMusicOn(_channel?.Name);
            audioSource.volume = AudioController.GetMusicVolume(_channel?.Name)
                                 * _currentVolume
                                 * _volumeMultiplier;
            audioSource.pitch = _currentPitch;
            if (b && !audioSource.mute)
                audioSource.Play();
        }

        [HorizontalGroup("h1"), Button, ShowIf("ShowBtns")]
        public void Play(SoundClip music)
        {
            _currentPitch = music.GetPitch();
            _currentVolume = music.GetVolume();
            _channel = music.Channel;
            if (audioSource != null)
                CheckSettings();
            if (audioSource == null)
                return;
            audioSource.clip = music.GetClip();
            audioSource.Play();
        }

        public void Play(AudioClip music)
        {
            _currentPitch = 1f;
            _currentVolume = 1f;
            _channel = null;
            if (audioSource != null)
                CheckSettings();
            if (audioSource == null)
                return;
            audioSource.clip = music;
            audioSource.Play();
        }

        /// <summary>
        /// Проверка, играет ли аудио
        /// </summary>
        /// <returns></returns>
        public bool IsPlay() => audioSource.isPlaying;

        [HorizontalGroup("h1"), Button, ShowIf("ShowBtns")]
        public void Stop()
        {
            if (audioSource == null)
                return;
            audioSource.Stop();
        }

        public void SetVolumeMultiplier(float value)
        {
            _volumeMultiplier = Mathf.Clamp01(value);
            CheckSettings();
        }

        public float GetVolumeMultiplier() => _volumeMultiplier;

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (audioSource == null)
                audioSource = GetComponent<AudioSource>();
        }

        private bool ShowBtns() => Application.isPlaying;
#endif
    }
}