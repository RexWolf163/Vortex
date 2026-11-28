using System.Collections.Generic;

namespace Vortex.Core.NetworkSystem.Model
{
    /// <summary>
    /// Адрес запроса до запуска: основа (имя хоста из настроек или полный URL), путь, параметры «имя → значение»
    /// в порядке добавления. Заполняется до запуска запроса; итоговый адрес собирает контроллер.
    /// </summary>
    public sealed class UrlData
    {
        private readonly List<KeyValuePair<string, object>> _parameters = new();

        /// <summary>Основа: имя хоста из настроек связи. <c>null</c>, если основа — полный URL.</summary>
        public string HostName { get; internal set; }

        /// <summary>Основа: полный URL. <c>null</c>, если основа — имя хоста из настроек.</summary>
        public string FullUrl { get; internal set; }

        /// <summary>Путь относительно основы. Берётся как есть: экранирует тот, кто его задал.</summary>
        public string Path { get; internal set; }

        /// <summary>Дополнительные параметры «имя → значение» в порядке добавления.</summary>
        public IReadOnlyList<KeyValuePair<string, object>> Parameters => _parameters;

        /// <summary>Создать пустой адрес: основу, путь и параметры задаёт сборка запроса.</summary>
        internal UrlData()
        {
        }

        /// <summary>Добавить параметр в конец списка.</summary>
        internal void AddParameter(string name, object value) =>
            _parameters.Add(new KeyValuePair<string, object>(name, value));
    }
}
