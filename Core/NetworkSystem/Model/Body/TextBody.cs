namespace Vortex.Core.NetworkSystem.Model.Body
{
    /// <summary>Тело-текст: строка и Content-Type (не задан — <see cref="DefaultContentType"/>).</summary>
    public sealed class TextBody : QueryBody
    {
        /// <summary>Content-Type текста, если не задан иной.</summary>
        public const string DefaultContentType = "text/plain; charset=utf-8";

        /// <summary>Текст тела.</summary>
        public string Text { get; }

        /// <summary>Content-Type тела.</summary>
        public string ContentType { get; }

        /// <summary>Создать тело-текст.</summary>
        /// <param name="text">Текст тела.</param>
        /// <param name="contentType">Content-Type; <c>null</c> или пусто — <see cref="DefaultContentType"/>.</param>
        public TextBody(string text, string contentType = null)
        {
            Text = text;
            ContentType = string.IsNullOrEmpty(contentType) ? DefaultContentType : contentType;
        }
    }
}
