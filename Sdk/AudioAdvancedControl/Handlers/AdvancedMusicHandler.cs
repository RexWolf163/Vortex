using UnityEngine;
using Vortex.Core.AudioSystem.Bus;
using Vortex.Core.AudioSystem.Controllers;
using Vortex.Core.AudioSystem.Model;
using Vortex.Sdk.AudioAdvancedControl.Model;
using Vortex.Unity.AudioSystem;

namespace Vortex.Sdk.AudioAdvancedControl.Handlers
{
    /// <summary>
    /// Управляемая выгружаемая музыка. Играет в слоте cover-музыки: основная тема на это время приглушается
    /// и возвращается после останова. Громкость — по общим настройкам музыки.
    ///
    /// Слот cover-музыки в приложении один: два одновременно играющих хэндлера (или хэндлер и другая система)
    /// перебивают друг друга. Вытесненный хэндлер замолкает и отпускает клип.
    ///
    /// Конец трека слот не сообщает, поэтому в режимах <see cref="AdvancedPlaybackMode.Random"/> и
    /// <see cref="AdvancedPlaybackMode.OneShot"/> хэндлер сам отсчитывает длительность клипа (паузы не считаются)
    /// и действует за время затухания до конца — смена и останов ложатся на штатное затухание аудиосистемы.
    /// Клип отпускается после затухания, а не в момент команды
    /// </summary>
    public class AdvancedMusicHandler : AdvancedAudioHandler
    {
        /// <summary>
        /// Сколько трек уже отыграл, секунды
        /// </summary>
        private float _elapsed;

        /// <summary>
        /// На какой секунде трек считается доигравшим
        /// </summary>
        private float _limit;

        /// <summary>
        /// Конец текущего трека уже отработан
        /// </summary>
        private bool _completed;

        /// <summary>
        /// Время затухания музыки в аудиосистеме, взятое на запуске трека: к моменту останова
        /// (выключение сцены, выход из приложения) самой аудиосистемы может уже не быть
        /// </summary>
        private float _fadeTime;

        protected override bool KeepPlayingWhileLoading => true;

        protected override float ReleaseDelay => _fadeTime;

        protected override AudioSampleWrapper StartPlayback(AudioClip clip, bool loop)
        {
            _fadeTime = AudioPlayer.MusicFadeTime;
            _elapsed = 0f;
            _limit = Mathf.Max(0f, clip.length - _fadeTime);
            _completed = false;
            return AudioController.PlayCoverMusicWithControl(clip);
        }

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

        /// <summary>
        /// Музыкальный трек «завершается» только вытеснением из слота — замолкаем и отпускаем клип
        /// </summary>
        protected override void OnPlaybackFinished() => StopTrack();

        private void Update()
        {
            if (_completed || Mode == AdvancedPlaybackMode.Index)
                return;

            var playback = Playback;
            if (playback == null || playback.State != PlaybackState.Playing)
                return;

            _elapsed += Time.unscaledDeltaTime;
            if (_elapsed < _limit)
                return;

            _completed = true;
            CompleteTrack();
        }
    }
}
