using System;
using System.Globalization;
using Vortex.Core.Extensions.ReactiveValues;
using Vortex.Core.LoggerSystem.Bus;
using Vortex.Core.LoggerSystem.Model;

namespace Vortex.Core.AudioSystem.Model
{
    public class AudioChannel : IReactiveData
    {
        public AudioChannel(string channelName) => Name = channelName;

        public event Action OnUpdateData;

        public string Name { get; }
        public float Volume { get; internal set; } = 1f;
        public bool Mute { get; internal set; } = false;

        internal void CallOnUpdate() => OnUpdateData?.Invoke();

        public string ToSave() => $"{Name}:{(Mute ? "N" : "Y")}:{Volume.ToString(CultureInfo.InvariantCulture)}";

        public void FromSave(string data)
        {
            var ar = data.Split(':');
            FromSave(ar);
        }

        public void FromSave(string[] data)
        {
            if (Name != data[0])
            {
                Log.Print(LogLevel.Error, "Audio channel name mismatch", this);
                return;
            }

            Mute = data[1] == "N";
            Volume = float.Parse(data[2], CultureInfo.InvariantCulture);
            OnUpdateData?.Invoke();
        }
    }
}