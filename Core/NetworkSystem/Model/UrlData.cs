using System;
using System.Collections.Generic;

namespace Vortex.Core.NetworkSystem
{
    /// <summary>
    /// Адрес запроса до запуска: основа (именованный хост из настроек ИЛИ полный URL), путь и дополнительные параметры.
    /// Путь берётся как есть (экранирует потребитель), склейка основы и пути — без нормализации слэшей.
    /// Значения параметров форматируются сразу при добавлении (инвариантная культура, enum по имени); экранирование — при сборке.
    /// Из строки неявно создаётся адрес с полным URL.
    /// </summary>
    public sealed class UrlData
    {
        private readonly List<KeyValuePair<string, string>> _parameters = new();

        /// <summary>Имя хоста из настроек. <c>null</c>, если задан полный URL.</summary>
        public string HostName { get; private set; }

        /// <summary>Полный URL. <c>null</c>, если задан именованный хост.</summary>
        public string AbsoluteUrl { get; private set; }

        /// <summary>Путь относительно основы (как есть).</summary>
        public string Path { get; private set; }

        /// <summary>Дополнительные параметры в порядке добавления (значения уже отформатированы, не экранированы).</summary>
        public IReadOnlyList<KeyValuePair<string, string>> Parameters => _parameters;

        private UrlData()
        {
        }

        /// <summary>Адрес из полного URL.</summary>
        public static UrlData FromUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                throw new ArgumentException("[Network] URL is empty.", nameof(url));
            return new UrlData { AbsoluteUrl = url };
        }

        /// <summary>Адрес из именованного хоста настроек и пути.</summary>
        public static UrlData FromHost(string hostName, string path = null)
        {
            if (string.IsNullOrWhiteSpace(hostName))
                throw new ArgumentException("[Network] Host name is empty.", nameof(hostName));
            return new UrlData { HostName = hostName, Path = path };
        }

        public static implicit operator UrlData(string url) => FromUrl(url);

        /// <summary>Добавить параметр. Повторный ключ перезаписывает значение.</summary>
        internal void SetParameter(string key, object value)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentException("[Network] Parameter key is empty.", nameof(key));

            var formatted = ValueFormatter.Format(key, value);
            for (var i = 0; i < _parameters.Count; i++)
            {
                if (_parameters[i].Key != key)
                    continue;
                _parameters[i] = new KeyValuePair<string, string>(key, formatted);
                return;
            }

            _parameters.Add(new KeyValuePair<string, string>(key, formatted));
        }

        /// <summary>Копия адреса (для копии цепочки).</summary>
        internal UrlData Clone()
        {
            var copy = new UrlData { HostName = HostName, AbsoluteUrl = AbsoluteUrl, Path = Path };
            copy._parameters.AddRange(_parameters);
            return copy;
        }
    }
}
