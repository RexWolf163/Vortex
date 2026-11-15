using UnityEngine;
using Vortex.Core.AudioSystem.Bus;
using Vortex.Core.AudioSystem.Controllers;
using Vortex.Core.AudioSystem.Model;
using Vortex.Sdk.AudioAdvancedControl.Model;

namespace Vortex.Sdk.AudioAdvancedControl.Handlers
{
    /// <summary>
    /// Управляемая выгружаемая музыка. Играет в слоте cover-музыки: основная тема на это время приглушается
    /// и возвращается после останова. Громкость — по общим настройкам музыки.
    ///
    /// Слот cover-музыки в приложении один: два одновременно играющих хэндлера (или хэндлер и другая система)
    /// перебивают друг друга. Вытесненный хэндлер замолкает и отпускает клип.
    ///
    /// Музыка в слоте зациклена всегда, поэтому конец трека приходит рестартом — уже после того, как клип
    /// пошёл на новый круг. В режимах <see cref="AdvancedPlaybackMode.IndexOneShot"/> и
    /// <see cref="AdvancedPlaybackMode.RandomOneShot"/> трек останавливается по нему же.
    /// Клип отпускается в момент команды: хвост затухания доигрывает из буфера кэша ассетов
    /// </summary>
    public class AdvancedMusicHandler : AdvancedAudioHandler
    {
        /// <summary>
        /// Слот cover-музыки сам сменяет прежний трек новым, с затуханием. Отдельный останов перед запуском
        /// вернул бы основную тему и оборвал бы затухание
        /// </summary>
        protected override bool ReplacedOnStart => true;

        /// <summary>
        /// Слот играет только зацикленно — <paramref name="loop"/> не используется
        /// </summary>
        protected override AudioSampleWrapper StartPlayback(AudioClip clip, bool loop) =>
            AudioController.PlayCoverMusicWithControl(clip);

        protected override void StopPlayback(AudioSampleWrapper playback)
        {
            switch (playback.State)
            {
                case PlaybackState.Pending:
                    //Музыка ещё не зазвучала (аудиосистема гасит основную тему) — снимаем отложенный старт
                    playback.Stop();
                    break;
                case PlaybackState.Paused:
                    //Аудиосистема останавливает только играющую музыку — с паузы её нужно снять
                    playback.Play();
                    AudioController.StopCoverMusic();
                    break;
                default:
                    AudioController.StopCoverMusic();
                    break;
            }
        }
    }
}
