using System;

namespace Vortex.Sdk.GallerySystem.View
{
    /// <summary>
    /// Runtime-only контейнер колбэков, передаётся в PoolItem одним объектом.
    /// Причина обёртки: <c>PoolItem.GetData&lt;T&gt;()</c> ищет по типу и возвращает
    /// первый попавшийся — два отдельных <c>Action</c>-поля в data-массиве
    /// были бы неразличимы.
    /// Не сериализуется, живёт только в рантайме.
    /// </summary>
    public class GalleryPoolCallbacks
    {
        /// <summary>Карточка вызывает на ховер/select-жесте.</summary>
        public Action OnFocus;

        /// <summary>Карточка вызывает после <c>entry.Show()</c> — уведомление обрамляющего UI.</summary>
        public Action OnShow;
    }
}
