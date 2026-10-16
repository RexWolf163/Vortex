using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine.AddressableAssets;
using Vortex.Unity.AssetCacheSystem.Models;
using Object = UnityEngine.Object;

namespace Vortex.Unity.AssetCacheSystem.Abstractions
{
    /// <summary>
    /// Контракт контроллера пакета AssetCacheSystem.
    /// Consumer-API: <see cref="Load{T}"/> и <see cref="Release"/>, плюс синхронный <see cref="LoadSync{T}"/>.
    /// Остальное — lifecycle, используемый шиной при bootstrap.
    /// </summary>
    public interface IAssetCacheController
    {
        /// <summary>Завершена ли инициализация контроллера.</summary>
        bool IsInitialized { get; }

        /// <summary>Runtime-модель пакета (для отладки/инспекции, мутация не поддерживается).</summary>
        AssetCacheModel Model { get; }

        /// <summary>Контроллер инициализирован.</summary>
        event Action OnInitialized;

        /// <summary>Контроллер очищен.</summary>
        event Action OnReleased;

        /// <summary>
        /// Инициализация контроллера. Идемпотентна.
        /// </summary>
        void Init();

        /// <summary>
        /// Очистка контроллера: освобождение всех handle'ов и сброс реестра. Идемпотентна.
        /// </summary>
        void Cleanup();

        /// <summary>
        /// Зарегистрировать <paramref name="owner"/> и асинхронно вернуть ассет по <paramref name="reference"/>.
        /// Если ассет уже в кэше (active или survivor) — мгновенно. Если идёт inflight — подключается к нему.
        /// Иначе запускается новая загрузка через <c>Addressables.LoadAssetAsync</c>.
        /// Параллельные запросы того же ref дедуплицируются в один Addressables-вызов.
        /// </summary>
        /// <typeparam name="T">Ожидаемый тип ассета. <see cref="UnityEngine.Object"/> и наследники.</typeparam>
        /// <param name="owner">Владелец. Не может быть null.</param>
        /// <param name="reference">AssetReference. Не может быть null.</param>
        /// <param name="ct">Токен waiter'а. Отмена waiter'а НЕ прерывает реальную загрузку — она нужна другим.</param>
        /// <param name="group">
        /// Группа кэша. <c>null</c>/пусто — дефолтная группа. Неизвестная — ошибка в лог и дефолтная группа.
        /// </param>
        /// <returns>Загруженный ассет.</returns>
        /// <exception cref="ArgumentNullException">owner или reference == null.</exception>
        /// <exception cref="OperationCanceledException">
        /// <paramref name="ct"/> отменён, либо владельца отпустили (<see cref="Release"/>) во время загрузки —
        /// ассет без удержания не возвращается.
        /// </exception>
        UniTask<T> Load<T>(object owner, AssetReference reference, CancellationToken ct = default,
            string group = null) where T : Object;

        /// <summary>
        /// Зарегистрировать <paramref name="owner"/> и синхронно вернуть ассет по <paramref name="reference"/>.
        /// Если ассет уже в кэше (active или survivor) — мгновенно.
        /// Иначе запускается новая загрузка через <c>Addressables.LoadAssetAsync</c>.
        /// </summary>
        /// <typeparam name="T">Ожидаемый тип ассета. <see cref="UnityEngine.Object"/> и наследники.</typeparam>
        /// <param name="owner">Владелец. Не может быть null.</param>
        /// <param name="reference">AssetReference. Не может быть null.</param>
        /// <param name="group">Группа кэша. См. <see cref="Load{T}"/>.</param>
        /// <returns>Загруженный ассет.</returns>
        /// <exception cref="ArgumentNullException">owner или reference == null.</exception>
        T LoadSync<T>(object owner, AssetReference reference, string group = null) where T : Object;

        /// <summary>
        /// Освободить все ассеты владельца в группе. Заодно sweep'ит (во всех группах)
        /// UnityEngine.Object-owner'ов, которые были тихо уничтожены без вызова Release.
        /// Ассеты без других активных владельцев группы уходят в её survivors (LRU). При переполнении
        /// survivors группы самые старые ассеты реально выгружаются через Addressables.Release.
        /// </summary>
        /// <param name="owner">Владелец. Не может быть null.</param>
        /// <param name="group">Группа, в которой владелец грузил. См. <see cref="Load{T}"/>.</param>
        /// <exception cref="ArgumentNullException">owner == null.</exception>
        void Release(object owner, string group = null);
    }
}