namespace Vortex.Core.NetworkSystem.Model.Body
{
    /// <summary>Бинарная часть multipart из памяти: имя файла, MIME, байты.</summary>
    public sealed class BytesPart : MultipartPart
    {
        /// <summary>Имя файла, передаваемое серверу.</summary>
        public string FileName { get; }

        /// <summary>MIME-тип содержимого.</summary>
        public string MimeType { get; }

        /// <summary>Содержимое. Массив не копируется — не изменять.</summary>
        public byte[] Bytes { get; }

        /// <summary>Создать бинарную часть из памяти.</summary>
        /// <param name="name">Имя части.</param>
        /// <param name="fileName">Имя файла, передаваемое серверу.</param>
        /// <param name="mimeType">MIME-тип содержимого.</param>
        /// <param name="bytes">Содержимое.</param>
        public BytesPart(string name, string fileName, string mimeType, byte[] bytes) : base(name)
        {
            FileName = fileName;
            MimeType = mimeType;
            Bytes = bytes;
        }
    }
}
