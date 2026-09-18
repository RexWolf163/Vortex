using UnityEngine;

namespace Vortex.Sdk.GallerySystem
{
    /// <summary>
    /// Контракт единицы контента галлереи. Реализуется на модели-Record
    /// (не на пресете-SO): <c>Database.GetRecords(typeof(IGalleryEntry))</c>
    /// возвращает именно модели.
    ///
    /// <c>Show</c>/<c>Hide</c> синхронные — если реализация запускает асинхронку,
    /// это её owner-scope. <c>GalleryView</c> ничего сверху не гарантирует.
    ///
    /// Разблокировка (mark в <c>RecordMarksBus</c>) ортогональна показу — этот
    /// контракт её не касается и не должен менять состояние меток.
    /// </summary>
    public interface IGalleryEntry
    {
        string GuidPreset { get; }
        Sprite Icon { get; }
        string Name { get; }

        void Show();
        void Hide();
    }
}
