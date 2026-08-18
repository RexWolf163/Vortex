using Vortex.Core.AudioSystem.Model;

namespace Vortex.Unity.AudioSystem
{
    public partial class AudioDriver
    {
        public void PlaySound(object sound, bool loop = false, string defaultChannel = null) =>
            AudioPlayer.PlaySound(sound, loop, defaultChannel);

        public AudioSampleWrapper PlaySoundWithControl(object sound, bool loop = false, string defaultChannel = null) =>
            AudioPlayer.PlaySound(sound, loop, defaultChannel, withControl: true);

        public void StopAllSounds(string channel = null) => AudioPlayer.StopAllSounds(channel);

        public void PlayMusic(object audioClip,
            bool fadingStart = true,
            bool fadingEnd = true,
            string defaultChannel = null) =>
            AudioPlayer.PlayMusic(audioClip, fadingStart, fadingEnd, defaultChannel);

        public AudioSampleWrapper PlayMusicWithControl(object audioClip,
            bool fadingStart = true,
            bool fadingEnd = true,
            string defaultChannel = null) =>
            AudioPlayer.PlayMusic(audioClip, fadingStart, fadingEnd, defaultChannel);

        public void StopMusic() => AudioPlayer.StopMusic();

        public void PlayCoverMusic(object audioClip,
            bool fadingStart = true,
            bool fadingEnd = true,
            string defaultChannel = null) =>
            AudioPlayer.PlayCoverMusic(audioClip, fadingStart, fadingEnd, defaultChannel);

        public AudioSampleWrapper PlayCoverMusicWithControl(object audioClip,
            bool fadingStart = true,
            bool fadingEnd = true,
            string defaultChannel = null) =>
            AudioPlayer.PlayCoverMusic(audioClip, fadingStart, fadingEnd, defaultChannel);

        public void StopCoverMusic() => AudioPlayer.StopCoverMusic();
    }
}
