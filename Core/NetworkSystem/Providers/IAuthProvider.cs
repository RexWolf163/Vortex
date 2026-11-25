using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Vortex.Core.NetworkSystem
{
    /// <summary>
    /// Провайдер авторизации: по токену добавляет заголовки доступа. Секретов не хранит: токен выдаёт источник,
    /// зарегистрированный кодом игры через <see cref="NetworkController.RegisterTokenSource"/> по ключу <see cref="TokenSourceKey"/>.
    /// </summary>
    public interface IAuthProvider
    {
        /// <summary>Ключ источника токена.</summary>
        string TokenSourceKey { get; }

        /// <summary>Добавить заголовки авторизации для токена.</summary>
        void ApplyHeaders(string token, IDictionary<string, string> headers);
    }

    /// <summary>
    /// Источник токена. <paramref name="forceNew"/> = <c>true</c> — после 401, нужен новый токен.
    /// Пустой результат означает «токена нет».
    /// </summary>
    public delegate UniTask<string> TokenSource(bool forceNew, CancellationToken cancellationToken);
}
