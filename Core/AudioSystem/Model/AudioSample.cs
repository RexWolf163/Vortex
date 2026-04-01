using Vortex.Core.DatabaseSystem.Model;

namespace Vortex.Core.AudioSystem.Model
{
    public abstract class AudioSample<T> : Record, IAudioSample where T : class
    {
        public override string GetDataForSave() => null;

        public override void LoadFromSaveData(string data)
        {
        }

        /// <summary>
        /// Длительность
        /// </summary>
        public float Duration { get; protected set; }

        /// <summary>
        /// Образец для воспроизведения
        /// Требуется типизация под движок
        /// </summary>
        public T Sample { get; protected set; }
    }
}