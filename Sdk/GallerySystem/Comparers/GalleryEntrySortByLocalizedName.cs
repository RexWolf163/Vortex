using System;
using Vortex.Core.LocalizationSystem;

namespace Vortex.Sdk.GallerySystem.Comparers
{
    /// <summary>
    /// Сортировка по локализованному имени. Ключ — <see cref="IGalleryEntry.Name"/>,
    /// перевод через <see cref="StringExt.TryTranslate"/>: если ключа нет, вернётся
    /// исходное имя — порядок деградирует к raw-сортировке, а не рушится в пустые строки.
    /// CurrentCulture — сортировка по правилам текущего языка.
    /// </summary>
    public sealed class GalleryEntrySortByLocalizedName : IGalleryEntryComparer
    {
        public int Compare(IGalleryEntry x, IGalleryEntry y)
        {
            if (ReferenceEquals(x, y)) return 0;
            if (x is null) return -1;
            if (y is null) return 1;
            var lx = x.Name.TryTranslate();
            var ly = y.Name.TryTranslate();
            return string.Compare(lx, ly, StringComparison.CurrentCultureIgnoreCase);
        }
    }
}
