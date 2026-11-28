namespace Vortex.Core.NetworkSystem.Model.Body
{
    /// <summary>
    /// Бинарная часть multipart из файла: имя файла, MIME, путь. Путь — строка для драйвера: существование
    /// файла проверяет драйвер при отправке.
    /// </summary>
    public sealed class FilePart : MultipartPart
    {
        /// <summary>Имя файла, передаваемое серверу.</summary>
        public string FileName { get; }

        /// <summary>MIME-тип содержимого.</summary>
        public string MimeType { get; }

        /// <summary>Путь к файлу с содержимым.</summary>
        public string FilePath { get; }

        /// <summary>Создать бинарную часть из файла.</summary>
        /// <param name="name">Имя части.</param>
        /// <param name="fileName">Имя файла, передаваемое серверу.</param>
        /// <param name="mimeType">MIME-тип содержимого.</param>
        /// <param name="filePath">Путь к файлу с содержимым.</param>
        public FilePart(string name, string fileName, string mimeType, string filePath) : base(name)
        {
            FileName = fileName;
            MimeType = mimeType;
            FilePath = filePath;
        }
    }
}
