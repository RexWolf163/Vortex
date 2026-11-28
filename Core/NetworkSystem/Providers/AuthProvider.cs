using System;
using System.Collections.Generic;

namespace Vortex.Core.NetworkSystem.Providers
{
    /// <summary>
    /// Способ предъявить токен серверу: ключ источника токена и заголовки авторизации для данного токена.
    /// К источнику токена обращается контроллер; провайдер токен не хранит. Данные настройки — в наследниках.
    /// </summary>
    [Serializable]
    public abstract class AuthProvider
    {
        /// <summary>Ключ, под которым код игры зарегистрировал источник токена.</summary>
        public abstract string TokenSourceKey { get; }

        /// <summary>Заголовки авторизации «имя → значение» для данного токена.</summary>
        public abstract IReadOnlyList<KeyValuePair<string, string>> GetHeaders(string token);
    }
}
