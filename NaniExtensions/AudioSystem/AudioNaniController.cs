using System.Collections.Generic;
using System.Linq;
using Naninovel;
using Vortex.Core.Extensions.LogicExtensions;
using Vortex.NaniExtensions.Core;
using AudioPlayer = Vortex.Unity.AudioSystem.AudioPlayer;

namespace Audio
{
    public static class AudioNaniController
    {
        private static float MusicFadeTime => AudioPlayer.MusicFadeTime;
        public static string PausedMusicPath { get; set; }

        public static void StopNaniMusic()
        {
            var source = new List<string>();
            NaniWrapper.AudioManager.GetPlayedBgm(source);
            if (source.Count == 0)
                return;
            PausedMusicPath = source.Last();
            if (!NaniWrapper.AudioManager.IsBgmPlaying(PausedMusicPath))
                return;
            NaniWrapper.AudioManager.StopBgm(PausedMusicPath, MusicFadeTime, new AsyncToken());
        }

        public static void PlayNaniMusic()
        {
            if (PausedMusicPath.IsNullOrWhitespace())
                return;
            NaniWrapper.AudioManager.PlayBgm(PausedMusicPath, 1f, MusicFadeTime);
            PausedMusicPath = string.Empty;
        }

        public static void StopNaniVoice() => NaniWrapper.AudioManager.StopVoice();
        public static void StopNaniSfx() => NaniWrapper.AudioManager.StopAllSfx();
    }
}