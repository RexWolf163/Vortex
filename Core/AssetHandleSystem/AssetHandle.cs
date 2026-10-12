using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Vortex.Core.AssetHandleSystem
{
    /// <summary>
    /// Абстрактный полиморфный указатель на ассет платформы с единым async-контрактом.
    /// Реализации — в платформенном слое: прямая ссылка (ассет живёт вместе с держателем)
    /// и ленивая подгрузка через механизм загрузки платформы.
    ///
    /// Consumer-код одинаков для любой реализации: <c>await handle.LoadAsync()</c>.
    /// Прямая ссылка возвращает ассет мгновенно, ленивая инициирует подгрузку. Выбор реализации
    /// делается в данных держателя (полиморфное поле) и не требует правки consumer'а.
    /// </summary>
    /// <typeparam name="T">Тип ассета. Реализации платформенного слоя могут сужать ограничение
    /// до базового типа ассетов своей платформы.</typeparam>
    [Serializable]
    public abstract class AssetHandle<T> where T : class
    {
        /// <summary>Синхронно проверить, доступен ли ассет для чтения через <see cref="Asset"/>.</summary>
        public abstract bool IsLoaded { get; }

        /// <summary>
        /// Синхронный доступ к загруженному ассету. Для прямой ссылки — всегда доступен
        /// (значение сохранённой ссылки). Для ленивой реализации — <c>null</c>
        /// до первого успешного <see cref="LoadAsync(CancellationToken)"/> или после <see cref="Release"/>.
        /// </summary>
        public abstract T Asset { get; }

        /// <summary>
        /// Единая точка получения ассета. Прямая ссылка — <c>UniTask.FromResult</c> мгновенно;
        /// ленивая реализация — делегирует подгрузку в underlying-механизм и кэширует результат.
        /// Повторный вызов до <see cref="Release"/> возвращает тот же экземпляр.
        /// </summary>
        public abstract UniTask<T> LoadAsync(CancellationToken ct);

        /// <summary>Перегрузка для потребителей, которым отмена не нужна.</summary>
        public UniTask<T> LoadAsync() => LoadAsync(CancellationToken.None);

        /// <summary>
        /// Отпустить ассет. Прямая ссылка — no-op (ссылка живёт с держателем).
        /// Ленивая реализация — освобождает underlying-ресурс; handle переиспользуем
        /// (см. XML-doc конкретной реализации). Идемпотентно.
        /// </summary>
        public abstract void Release();
    }
}
