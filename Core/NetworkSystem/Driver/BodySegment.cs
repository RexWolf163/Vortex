namespace Vortex.Core.NetworkSystem.Driver
{
    /// <summary>Кусок подготовленного тела: байты или путь к файлу.</summary>
    public sealed class BodySegment
    {
        /// <summary>Байты сегмента. <c>null</c>, если сегмент — файл. Массив не копируется — не изменять.</summary>
        public byte[] Bytes { get; }

        /// <summary>Путь к файлу сегмента. <c>null</c>, если сегмент — байты.</summary>
        public string FilePath { get; }

        private BodySegment(byte[] bytes, string filePath)
        {
            Bytes = bytes;
            FilePath = filePath;
        }

        /// <summary>Сегмент из байтов.</summary>
        internal static BodySegment FromBytes(byte[] bytes) => new(bytes, null);

        /// <summary>Сегмент из файла.</summary>
        internal static BodySegment FromFile(string filePath) => new(null, filePath);
    }
}
