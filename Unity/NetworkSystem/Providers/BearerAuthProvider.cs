using System;
using System.Collections.Generic;
using UnityEngine;
using Vortex.Core.NetworkSystem;

namespace Vortex.Unity.NetworkSystem.Providers
{
    /// <summary>
    /// Провайдер авторизации «Bearer»: заголовок <c>Authorization: Bearer &lt;токен&gt;</c> (имя заголовка и схема
    /// настраиваются). Токен выдаёт источник, зарегистрированный кодом игры по ключу
    /// (<see cref="NetworkController.RegisterTokenSource"/>). Секретов в ассете нет.
    /// </summary>
    [Serializable]
    public class BearerAuthProvider : IAuthProvider
    {
        [SerializeField, Tooltip("Ключ источника токена, который регистрирует код игры.")]
        private string tokenSourceKey;

        [SerializeField]
        private string headerName = "Authorization";

        [SerializeField, Tooltip("Префикс перед токеном. Пусто — токен без префикса.")]
        private string scheme = "Bearer";

        public string TokenSourceKey => tokenSourceKey;

        public void ApplyHeaders(string token, IDictionary<string, string> headers) =>
            headers[headerName] = string.IsNullOrEmpty(scheme) ? token : scheme + " " + token;
    }
}
