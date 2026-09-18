using System.Collections.Generic;

namespace Vortex.Sdk.GallerySystem
{
    /// <summary>
    /// Маркёр-интерфейс над <see cref="IComparer{T}"/> для сортировщиков галлереи.
    /// Существует, чтобы reflection-скан в редакторе <see cref="GalleryView"/>
    /// не тянул в выпадашку все <c>IComparer&lt;&gt;</c> в домене.
    /// Consumer-пакеты добавляют свои реализации — они автоматически попадают
    /// в <c>sorter</c>-выпадашку.
    /// </summary>
    public interface IGalleryEntryComparer : IComparer<IGalleryEntry> { }
}
