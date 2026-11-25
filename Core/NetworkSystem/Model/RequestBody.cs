using System;
using System.Collections.Generic;

namespace Vortex.Core.NetworkSystem
{
    /// <summary>Тело запроса. Вид задаётся наследником; подготовку к отправке выполняет контроллер.</summary>
    public abstract class RequestBody
    {
        internal RequestBody()
        {
        }
    }

    /// <summary>Объект, сериализуемый провайдером тела (по маске URL). Для GET/HEAD раскладывается в параметры адреса.</summary>
    public sealed class ObjectBody : RequestBody
    {
        public object Value { get; }

        public ObjectBody(object value) =>
            Value = value ?? throw new ArgumentNullException(nameof(value), "[Network] Object body is null.");
    }

    /// <summary>Сырые байты с обязательным Content-Type.</summary>
    public sealed class BytesBody : RequestBody
    {
        public byte[] Bytes { get; }
        public string ContentType { get; }

        public BytesBody(byte[] bytes, string contentType)
        {
            Bytes = bytes ?? throw new ArgumentNullException(nameof(bytes), "[Network] Bytes body is null.");
            if (string.IsNullOrWhiteSpace(contentType))
                throw new ArgumentException("[Network] Content-Type is required for a bytes body.", nameof(contentType));
            ContentType = contentType;
        }
    }

    /// <summary>Текст в UTF-8. Content-Type по умолчанию — <c>text/plain; charset=utf-8</c>.</summary>
    public sealed class TextBody : RequestBody
    {
        public string Text { get; }
        public string ContentType { get; }

        public TextBody(string text, string contentType = null)
        {
            Text = text ?? throw new ArgumentNullException(nameof(text), "[Network] Text body is null.");
            ContentType = string.IsNullOrWhiteSpace(contentType) ? "text/plain; charset=utf-8" : contentType;
        }
    }

    /// <summary>Форма <c>application/x-www-form-urlencoded</c>. Значения форматируются как параметры адреса.</summary>
    public sealed class FormBody : RequestBody
    {
        private readonly List<KeyValuePair<string, string>> _fields = new();

        public IReadOnlyList<KeyValuePair<string, string>> Fields => _fields;

        public FormBody(IEnumerable<KeyValuePair<string, object>> fields)
        {
            if (fields == null)
                throw new ArgumentNullException(nameof(fields), "[Network] Form fields are null.");
            foreach (var field in fields)
            {
                if (string.IsNullOrEmpty(field.Key))
                    throw new ArgumentException("[Network] Form field key is empty.", nameof(fields));
                _fields.Add(new KeyValuePair<string, string>(field.Key, ValueFormatter.Format(field.Key, field.Value)));
            }
        }
    }

    /// <summary>
    /// <c>multipart/form-data</c>: текстовые и бинарные части. Бинарная часть — байты в памяти или файл с диска
    /// (потоковая отправка — возможность драйвера). Граница генерируется модулем.
    /// </summary>
    public sealed class MultipartBody : RequestBody
    {
        private readonly List<MultipartPart> _parts = new();

        public IReadOnlyList<MultipartPart> Parts => _parts;

        public MultipartBody AddText(string name, string value)
        {
            _parts.Add(new MultipartPart(name, value ?? string.Empty, null, null, null, null));
            return this;
        }

        public MultipartBody AddBytes(string name, string fileName, string mimeType, byte[] bytes)
        {
            if (bytes == null)
                throw new ArgumentNullException(nameof(bytes), "[Network] Multipart part bytes are null.");
            _parts.Add(new MultipartPart(name, null, fileName, MimeOrDefault(mimeType), bytes, null));
            return this;
        }

        public MultipartBody AddFile(string name, string fileName, string mimeType, string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("[Network] Multipart file path is empty.", nameof(filePath));
            _parts.Add(new MultipartPart(name, null, fileName, MimeOrDefault(mimeType), null, filePath));
            return this;
        }

        private static string MimeOrDefault(string mimeType) =>
            string.IsNullOrWhiteSpace(mimeType) ? "application/octet-stream" : mimeType;
    }

    /// <summary>Часть multipart: текстовая (<see cref="Text"/>) или бинарная (<see cref="Bytes"/> либо <see cref="FilePath"/>).</summary>
    public sealed class MultipartPart
    {
        public string Name { get; }
        public string Text { get; }
        public string FileName { get; }
        public string MimeType { get; }
        public byte[] Bytes { get; }
        public string FilePath { get; }

        public bool IsText => Bytes == null && FilePath == null;

        internal MultipartPart(string name, string text, string fileName, string mimeType, byte[] bytes, string filePath)
        {
            if (string.IsNullOrEmpty(name))
                throw new ArgumentException("[Network] Multipart part name is empty.", nameof(name));
            Name = name;
            Text = text;
            FileName = fileName;
            MimeType = mimeType;
            Bytes = bytes;
            FilePath = filePath;
        }
    }
}
