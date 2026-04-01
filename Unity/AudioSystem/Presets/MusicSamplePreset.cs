using System;
using Sirenix.OdinInspector;
using UnityEngine;
using Vortex.Core.DatabaseSystem.Model.Enums;
using Vortex.Unity.AudioSystem.Attributes;
using Vortex.Unity.AudioSystem.Model;
using Vortex.Unity.DatabaseSystem.Presets;

namespace Vortex.Unity.AudioSystem.Presets
{
    [CreateAssetMenu(fileName = "MusicSample", menuName = "Database/MusicSample")]
    public class MusicSamplePreset : RecordPreset<Music>
    {
        [SerializeField, OnValueChanged("CalcDuration")]
        private AudioClip audioClip;

        [SerializeField, Range(-3f, 3f), OnValueChanged("CalcDuration")]
        private float pitch = 1f;

        [SerializeField, Range(0f, 1f)] private float value = 1f;

        [SerializeField, DisplayAsString] private float duration;

        [SerializeField, AudioChannelName] private string channel;

        public SoundClip Sample => new(new[] { audioClip }, new Vector2(pitch, pitch), new Vector2(value, value), true,
            channel);

        public float Duration => duration;

#if UNITY_EDITOR
        private void CalcDuration() =>
            duration = pitch == 0 ? float.MaxValue : audioClip.length / Mathf.Abs(pitch);

        private GameObject _testObject;

        private bool IsPlay() => _testObject != null;

        private void OnValidate() => type = RecordTypes.Singleton;

        [Button, HideIf("IsPlay")]
        private void TestSound()
        {
            var pitch = this.pitch == 0 ? 0.1f : Mathf.Abs(this.pitch);
            var volume = value;
            _testObject = new GameObject();
            var audio = _testObject.AddComponent<AudioSource>();
            _testObject.AddComponent<AudioListener>();
            audio.pitch = pitch;
            audio.PlayOneShot(audioClip, volume);
            var sd = _testObject.AddComponent<SelfDestroy>();
            sd.TargetTime = DateTime.Now.AddSeconds(audioClip.length / pitch);
        }

        [Button, HideIf("@!IsPlay()")]
        private void StopSound()
        {
            if (Application.isPlaying)
                Destroy(_testObject);
            else
                DestroyImmediate(_testObject);

            _testObject = null;
        }

        [ExecuteInEditMode]
        private class SelfDestroy : MonoBehaviour
        {
            public DateTime TargetTime = DateTime.Now + new TimeSpan(0, 0, 10);

            private void Update()
            {
                if (DateTime.Now <= TargetTime)
                    return;
                if (!Application.isPlaying)
                    DestroyImmediate(gameObject);
                else
                    Destroy(gameObject);
            }
        }

#endif
    }
}