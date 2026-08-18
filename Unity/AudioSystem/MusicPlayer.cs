using Sirenix.OdinInspector;
using UnityEngine;
using Vortex.Core.AudioSystem.Bus;
using Vortex.Core.AudioSystem.Controllers;
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

        /// <summary>
        /// Текущий хэндл трека. Единый владелец: на новый трек предыдущий завершается (<see cref="RetireCurrent"/>),
        /// один враппер на один играющий источник — без дублей и орфанов.
        /// </summary>
        private AudioSampleWrapper _currentWrapper;

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

        /// <summary>
        /// Подготовить хэндл нового трека (в состоянии Pending) и завершить предыдущий. Источник стабилен,
        /// поэтому хэндл валиден сразу — даже если реальный старт (<see cref="Play(SoundClip)"/>) произойдёт
        /// позже (после фейда). Реальное аудио стартует уже через <c>wrapper.Play()</c>.
        /// </summary>
        public AudioSampleWrapper Prepare(AudioClip clip)
        {
            RetireCurrent();
            _currentWrapper = new MusicWrapper(audioSource, clip);
            return _currentWrapper;
        }

        [HorizontalGroup("h1"), Button, ShowIf("ShowBtns")]
        public void Play(SoundClip music)
        {
            _currentPitch = music.GetPitch();
            _currentVolume = music.GetVolume();
            _channel = music.Channel;
            if (audioSource == null)
                return;
            CheckSettings();
            var clip = music.GetClip();
            audioSource.clip = clip;
            // В штатном потоке враппер уже создан через Prepare; guard — для самостоятельного вызова (кнопка).
            _currentWrapper ??= new MusicWrapper(audioSource, clip);
            _currentWrapper.Play(); // Pending → Playing (+ audioSource.Play())
        }

        public void Play(AudioClip music)
        {
            _currentPitch = 1f;
            _currentVolume = 1f;
            _channel = null;
            if (audioSource == null)
                return;
            CheckSettings();
            audioSource.clip = music;
            Prepare(music); // standalone-путь: сам готовит хэндл (и завершает предыдущий)
            _currentWrapper.Play();
        }

        /// <summary>
        /// Проверка, играет ли аудио
        /// </summary>
        /// <returns></returns>
        public bool IsPlay() => audioSource.isPlaying;

        /// <summary>Внешний стоп: завершить хэндл и остановить источник.</summary>
        [HorizontalGroup("h1"), Button, ShowIf("ShowBtns")]
        public void Stop()
        {
            if (audioSource == null)
                return;
            RetireCurrent();
            audioSource.Stop();
        }

        /// <summary>
        /// Остановить только источник, НЕ трогая текущий хэндл. Для внутренних фейд-переходов, где хэндл
        /// нового трека уже подготовлен (<see cref="Prepare"/>) и его завершать нельзя.
        /// </summary>
        public void StopAudio()
        {
            if (audioSource == null)
                return;
            audioSource.Stop();
        }

        private void RetireCurrent()
        {
            if (_currentWrapper == null)
                return;
            _currentWrapper.Finish(); // OnFinished + State=Finished + отвязка (источник не глушим)
            _currentWrapper = null;
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