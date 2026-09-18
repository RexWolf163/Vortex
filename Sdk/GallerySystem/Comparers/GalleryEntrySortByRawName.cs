using System;

namespace Vortex.Sdk.GallerySystem.Comparers
{
    /// <summary>
    /// Сортировка по <see cref="IGalleryEntry.Name"/> — имя как в инспекторе, до локализации.
    /// InvariantCulture — порядок стабилен на любой пользовательской локали.
    /// </summary>
    public sealed class GalleryEntrySortByRawName : IGalleryEntryComparer
    {
        public int Compare(IGalleryEntry x, IGalleryEntry y)
        {
            if (ReferenceEquals(x, y)) return 0;
            if (x is null) return -1;
            if (y is null) return 1;
            return string.Compare(x.Name, y.Name, StringComparison.InvariantCultureIgnoreCase);
        }
    }
}
