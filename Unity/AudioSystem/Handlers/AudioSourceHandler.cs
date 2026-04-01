using Sirenix.OdinInspector;
using UnityEngine;
using Vortex.Core.AudioSystem.Bus;
using Vortex.Core.System.Abstractions;
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
            audioSource.PlayOneShot(audioClip);
        }

        [HorizontalGroup("h1"), Button, ShowIf("ShowBtns")]
        private void Stop() => audioSource.Stop();

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