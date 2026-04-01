using Cysharp.Threading.Tasks;
using UnityEngine;
using Vortex.Core.AudioSystem.Bus;
using Vortex.Core.Extensions.LogicExtensions;
using Vortex.Unity.AppSystem.System.TimeSystem;
using Vortex.Unity.AudioSystem.Model;
using Vortex.Unity.DatabaseSystem.Attributes;

namespace Vortex.Unity.AudioSystem.Handlers
{
    public class MusicHandler : MonoBehaviour
    {
        [SerializeField, DbRecord(typeof(Music))]
        private string audioSample;

        private Music _sample;

        [SerializeField] private bool fadeStart = true;
        [SerializeField] private bool fadeEnd = true;

        [SerializeField] private bool isCoverMusic = false;

        private bool _isPlay;

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
            _sample = AudioController.GetSample(audioSample) as Music;
            if (_sample == null)
                Debug.LogError(audioSample.IsNullOrWhitespace()
                    ? "[MusicHandler] Empty Sample data."
                    : "[MusicHandler] Incorrect Sample data.");
            if (isActiveAndEnabled)
                OnEnable();
        }

        private void OnEnable()
        {
            if (_sample == null)
                return;

            TimeController.RemoveCall(this);
            PlayMusic().Forget(Debug.LogException);
        }

        private void OnDisable()
        {
            if (_sample == null)
                return;

            TimeController.RemoveCall(this);
            //Отложенный вызов для обхода "горячего рестарта"
            TimeController.Call(StopMusic, this);
        }

        private async UniTask PlayMusic()
        {
            if (_isPlay)
                return;
            _isPlay = true;
            await UniTask.DelayFrame(2);
            if (isCoverMusic)
                AudioController.PlayCoverMusic(_sample, fadeStart, fadeEnd);
            else
                AudioController.PlayMusic(_sample, fadeStart, fadeEnd);
        }

        private void StopMusic()
        {
            if (!_isPlay)
                return;
            _isPlay = false;
            if (isCoverMusic)
                AudioController.StopCoverMusic();
            else
                AudioController.StopMusic();
        }
    }
}