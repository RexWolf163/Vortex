using Sirenix.OdinInspector;
using UnityEngine;
using Vortex.Core.AudioSystem.Bus;
using Vortex.Core.AudioSystem.Controllers;
using Vortex.Core.AudioSystem.Model;
using Vortex.Core.System.Abstractions;
using Vortex.Unity.AppSystem.System.TimeSystem;
using Vortex.Unity.AudioSystem.Model;

namespace Vortex.Unity.AudioSystem.Handlers
{
    [RequireComponent(typeof(AudioSource))]
    public class AudioSourceHandler : MonoBehaviour
    {
        [ReadOnly, ShowInInspector, HideLabel] private string _clipName;

        [SerializeField] private AudioSource audioSource;
        [SerializeField] private GameObject dataStorageObject;

        private IDataStorage _dataStorage;

        private SoundClip _sound;

        /// <summary>Хэндл управления, если звук запущен через PlaySoundWithControl (иначе null).</summary>
        private SoundWrapper _wrapper;

        private float _currentVolume;
        private float _currentPitch;

        private void CheckSettings()
        {
            audioSource.mute = !AudioController.GetSoundOn(_sound.Channel?.Name);
            audioSource.volume = AudioController.GetSoundVolume(_sound.Channel?.Name) * _currentVolume;
            audioSource.pitch = _currentPitch;
            audioSource.loop = _sound.IsLoop();
        }

        private void Awake() => _dataStorage = dataStorageObject.GetComponent<IDataStorage>();

        private void OnEnable() => Play();

        private void OnDisable() => Stop();

        [HorizontalGroup("h1"), Button, ShowIf("ShowBtns")]
        private void Play()
        {
            _sound = _dataStorage.GetData<SoundClip>();
            if (_sound == null)
                return;
            var audioClip = _sound.GetClip();
            _clipName = $"«{audioClip.name}»";
            _currentPitch = _sound.GetPitch();
            _currentVolume = _sound.GetVolume();
            CheckSettings();
            _wrapper = _dataStorage.GetData<SoundWrapper>();
            if (_wrapper != null)
            {
                _wrapper.Init(audioSource, () => gameObject.SetActive(false));
                _wrapper.Play();
                return;
            }

            if (_sound.IsLoop())
            {
                audioSource.loop = true;
                audioSource.clip = audioClip;
                audioSource.Play();
            }
            else
            {
                audioSource.clip = null;
                audioSource.loop = false;
                audioSource.PlayOneShot(audioClip);
                TimeController.Call(() => gameObject.SetActive(false), audioClip.length / Mathf.Abs(_currentPitch), this);
            }
        }

        [HorizontalGroup("h1"), Button, ShowIf("ShowBtns")]
        private void Stop()
        {
            TimeController.RemoveCall(this);
            if (_wrapper != null)
            {
                if (_wrapper.State != PlaybackState.Finished)
                    _wrapper.Stop();
                _wrapper = null;
                return;
            }

            audioSource.Stop();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (audioSource == null)
                audioSource = GetComponent<AudioSource>();
            if (dataStorageObject == null)
                return;
            _dataStorage = dataStorageObject.GetComponent<IDataStorage>();

            if (_dataStorage == null)
                dataStorageObject = null;
        }

        private bool ShowBtns() => Application.isPlaying;

#endif
    }
}