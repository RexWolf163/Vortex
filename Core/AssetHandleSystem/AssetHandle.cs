using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Vortex.Core.AssetHandleSystem
{
    /// <summary>
    /// Абстрактный полиморфный указатель на Unity-ассет с единым async-контрактом.
    /// Реализации:
    /// <list type="bullet">
    ///   <item><c>DirectAssetHandle&lt;T&gt;</c> — прямая ссылка, ассет живёт с пресетом.</item>
    ///   <item><c>AddressableAssetHandle&lt;T&gt;</c> — lazy через Addressables (Unity-слой,
    ///   собирается только при <c>ENABLE_ADDRESSABLES</c>).</item>
    /// </list>
    ///
    /// Consumer-код одинаков для обеих реализаций: <c>await handle.LoadAsync()</c>.
    /// Direct возвращает мгновенно, Lazy инициирует подгрузку. Переключение реализации
    /// в инспекторе через Odin <c>[SerializeReference]</c>-picker не требует правки consumer'а.
    ///
    /// Хранится в пресетах как <c>[SerializeReference] AssetHandle&lt;T&gt; _field</c>.
    /// </summary>
    /// <typeparam name="T">Тип Unity-ассета. Ограничение <c>UnityEngine.Object</c> обусловлено
    /// сериализуемостью полиморфного поля.</typeparam>
    [Serializable]
    public abstract class AssetHandle<T> where T : UnityEngine.Object
    {
        /// <summary>Синхронно проверить, доступен ли ассет для чтения через <see cref="Asset"/>.</summary>
        public abstract bool IsLoaded { get; }

        /// <summary>
        /// Синхронный доступ к загруженному ассету. Для <c>DirectAssetHandle</c> — всегда доступен
        /// (значение сериализованной ссылки). Для <c>AddressableAssetHandle</c> — <c>null</c>
        /// до первого успешного <see cref="LoadAsync(CancellationToken)"/> или после <see cref="Release"/>.
        /// </summary>
        public abstract T Asset { get; }

        /// <summary>
        /// Единая точка получения ассета. Direct — <c>UniTask.FromResult</c> мгновенно;
        /// Lazy — делегирует подгрузку в underlying-механизм и кэширует результат.
        /// Повторный вызов до <see cref="Release"/> возвращает тот же экземпляр.
        /// </summary>
        public abstract UniTask<T> LoadAsync(CancellationToken ct);

        /// <summary>Перегрузка для потребителей, которым отмена не нужна.</summary>
        public UniTask<T> LoadAsync() => LoadAsync(CancellationToken.None);

        /// <summary>
        /// Отпустить ассет. Direct — no-op (прямая ссылка живёт с пресетом).
        /// Lazy — освобождает underlying-handle и переводит handle в терминальное состояние
        /// (см. XML-doc конкретной реализации). Идемпотентно.
        /// </summary>
        public abstract void Release();
    }
}
