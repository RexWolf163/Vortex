using System;

namespace Vortex.Core.NetworkSystem.Model.Body
{
    /// <summary>Тело-байты: готовые байты и их Content-Type (обязателен).</summary>
    public sealed class BytesBody : QueryBody
    {
        /// <summary>Байты тела. Массив не копируется — не изменять.</summary>
        public byte[] Bytes { get; }

        /// <summary>Content-Type тела.</summary>
        public string ContentType { get; }

        /// <summary>Создать тело-байты. Content-Type не задан — ошибка программиста (fast-fail).</summary>
        /// <param name="bytes">Байты тела.</param>
        /// <param name="contentType">Content-Type тела, обязателен.</param>
        public BytesBody(byte[] bytes, string contentType)
        {
            if (string.IsNullOrEmpty(contentType))
                throw NetworkTools.Fail(
                    new ArgumentException("У тела-байтов не задан Content-Type.", nameof(contentType)),
                    nameof(BytesBody));
            Bytes = bytes;
            ContentType = contentType;
        }
    }
}
