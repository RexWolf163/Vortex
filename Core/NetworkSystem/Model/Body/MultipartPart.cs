namespace Vortex.Core.NetworkSystem.Model.Body
{
    /// <summary>
    /// Часть multipart с именем: <see cref="TextPart"/>, <see cref="BytesPart"/> или <see cref="FilePart"/>.
    /// Набор видов закрыт.
    /// </summary>
    public abstract class MultipartPart
    {
        /// <summary>Имя части.</summary>
        public string Name { get; }

        private protected MultipartPart(string name)
        {
            Name = name;
        }
    }
}
