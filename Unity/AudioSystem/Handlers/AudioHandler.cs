using Sirenix.OdinInspector;
using UnityEngine;
using Vortex.Core.AudioSystem.Bus;
using Vortex.Core.Extensions.LogicExtensions;
using Vortex.Unity.AppSystem.System.TimeSystem;
using Vortex.Unity.AudioSystem.Attributes;
using Vortex.Unity.AudioSystem.Model;
using Vortex.Unity.DatabaseSystem.Attributes;

namespace Vortex.Unity.AudioSystem.Handlers
{
    /// <summary>
    /// Компонент вызова звука.
    /// Может работать либо с личным AudioSource, либо ретранслировать запрос на AudioPlayer, при отсутствии AudioSource
    /// </summary>
    public class AudioHandler : MonoBehaviour
    {
        [SerializeField] private AudioSource audioSource;

        [SerializeField, DbRecord(typeof(Sound))]
        private string audioSample;

        [SerializeField, AudioChannelName] private string channel;

        private Sound _sample;

        private SoundClip _sound;
        private float _currentVolume;
        private float _currentPitch;

        private float _volumeMultiplier;

        private bool _isInit;

        private void Awake()
        {
            AudioController.OnInit += OnInit;
        }

        private void OnDestroy()
        {
            AudioController.OnInit -= OnInit;
            TimeController.RemoveCall(this);
        }

        private void OnInit() => TimeController.Accumulate(Init, this);


        private void Init()
        {
            _isInit = true;
            _sample = AudioController.GetSample(audioSample) as Sound;
            if (_sample == null)
            {
                Debug.LogError(audioSample.IsNullOrWhitespace()
                    ? "[AudioHandler] Empty Sample data."
                    : "[AudioHandler] Incorrect Sample data.");
                return;
            }

            _sound = _sample.Sample;
        }

        private void OnEnable()
        {
            if (!_isInit)
                Init();
            if (audioSource == null || (audioSource.clip == null && audioSample.IsNullOrWhitespace()))
                return;

            _volumeMultiplier = 1;
            AudioController.OnSettingsChanged += CheckSettings;
            CheckSettings();
        }

        private void OnDisable()
        {
            if (audioSource == null || (audioSource.clip == null && _sample == null))
                return;
            AudioController.OnSettingsChanged -= CheckSettings;
            Stop();
        }

        private void CheckSettings()
        {
            audioSource.mute = !AudioController.GetSoundOn(channel);
            audioSource.volume = AudioController.GetSoundVolume(channel) * _currentVolume * _volumeMultiplier;
            audioSource.pitch = _currentPitch;
        }

        [HorizontalGroup("h1"), Button, ShowIf("ShowBtns")]
        public void Play()
        {
            _currentPitch = _sound.GetPitch();
            _currentVolume = _sound.GetVolume();
            if (audioSource != null)
                CheckSettings();
            if (audioSource == null)
                AudioController.PlaySound(_sample);
            else
                audioSource.PlayOneShot(_sound.GetClip());
        }

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