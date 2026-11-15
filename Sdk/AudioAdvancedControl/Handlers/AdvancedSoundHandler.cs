using UnityEngine;
using Vortex.Core.AudioSystem.Bus;
using Vortex.Core.AudioSystem.Controllers;
using Vortex.Core.AudioSystem.Model;
using Vortex.Unity.AudioSystem.Attributes;
using Vortex.Unity.AudioSystem.Model;

namespace Vortex.Sdk.AudioAdvancedControl.Handlers
{
    /// <summary>
    /// Управляемый выгружаемый звук (эмбиент, фоновые петли, длинные эффекты). Играет как обычный звук
    /// аудиосистемы; громкость — по каналу громкости из настройки
    /// </summary>
    public class AdvancedSoundHandler : AdvancedAudioHandler
    {
        [SerializeField, AudioChannelName, Tooltip("Канал громкости.")]
        private string channel;

        protected override AudioSampleWrapper StartPlayback(AudioClip clip, bool loop) =>
            AudioController.PlaySoundWithControl(new SoundClipFixed(clip, loop, channel), loop);

        protected override void StopPlayback(AudioSampleWrapper playback) => playback.Stop();
    }
}
