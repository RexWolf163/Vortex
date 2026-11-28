namespace Vortex.Core.NetworkSystem.Model.Body
{
    /// <summary>Текстовая часть multipart: значение.</summary>
    public sealed class TextPart : MultipartPart
    {
        /// <summary>Значение части.</summary>
        public string Value { get; }

        /// <summary>Создать текстовую часть.</summary>
        /// <param name="name">Имя части.</param>
        /// <param name="value">Значение части.</param>
        public TextPart(string name, string value) : base(name)
        {
            Value = value;
        }
    }
}
