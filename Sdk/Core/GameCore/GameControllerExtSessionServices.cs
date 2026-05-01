using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Vortex.Sdk.Core.GameCore
{
    /// <summary>
    /// Реестр и ожидание готовности сервисов игровой сессии.
    /// Используется GameController перед переходом в Play после NewGame/OnLoad.
    /// </summary>
    public partial class GameController
    {
        private const int PollIntervalMs = 100;

        private static readonly HashSet<IGameSessionService> SessionServices = new();

        /// <summary>
        /// Регистрирует сервис, готовности которого должен дождаться GameController
        /// перед переходом в Play после NewGame/OnLoad.
        /// </summary>
        public static void RegisterSessionService(IGameSessionService service)
        {
            if (service != null) SessionServices.Add(service);
        }

        /// <summary>
        /// Снимает сервис с регистрации.
        /// </summary>
        public static void UnregisterSessionService(IGameSessionService service)
        {
            if (service != null) SessionServices.Remove(service);
        }

        /// <summary>
        /// Ожидание готовности всех зарегистрированных сервисов сессии.
        /// Возврат — только когда все готовы или сработал cancellationToken.
        /// Тайм-аута нет: fail-fast, повисший loader на тяжёлом сервисе предпочтительнее
        /// тихого входа в Play с неготовым движком.
        /// </summary>
        internal static async UniTask WaitForSessionServices(CancellationToken cancellationToken)
        {
            if (SessionServices.Count == 0) return;

            string lastReportedName = null;

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                IGameSessionService pending = null;
                foreach (var service in SessionServices)
                {
                    if (service.IsReady) continue;
                    pending = service;
                    break;
                }

                if (pending == null) return;

                if (pending.Name != lastReportedName)
                {
                    Debug.Log($"[GameController] Awaiting session service: {pending.Name}");
                    lastReportedName = pending.Name;
                }

                await UniTask.Delay(PollIntervalMs, true, cancellationToken: cancellationToken);
            }
        }
    }
}
