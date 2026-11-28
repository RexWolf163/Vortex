using System.Collections.Generic;
using System.Linq;

namespace Vortex.Core.NetworkSystem.Model
{
    /// <summary>Одно окружение: ключ и адреса хостов «имя → адрес».</summary>
    public sealed class NetworkEnvironment
    {
        private readonly KeyValuePair<string, string>[] _hosts;

        /// <summary>Ключ окружения.</summary>
        public string Key { get; }

        /// <summary>
        /// Адреса хостов «имя → адрес» в порядке задания. Список, а не словарь: повтор имени — ошибка
        /// конфигурации, её находит проверка настроек.
        /// </summary>
        public IReadOnlyList<KeyValuePair<string, string>> Hosts => _hosts;

        /// <summary>Создать окружение.</summary>
        /// <param name="key">Ключ окружения.</param>
        /// <param name="hosts">Адреса хостов «имя → адрес»; список копируется.</param>
        public NetworkEnvironment(string key, IEnumerable<KeyValuePair<string, string>> hosts)
        {
            Key = key;
            _hosts = hosts.ToArray();
        }
    }
}
