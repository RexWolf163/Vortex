using Vortex.Core.AudioSystem.Bus;
using Vortex.Core.AudioSystem.Model;

namespace Vortex.Core.AudioSystem.Controllers
{
    public static class AudioChannelController
    {
        public static void SetVolume(this AudioChannel channel, float volume) =>
            AudioController.SetChVolume(channel.Name, volume);
    }
}