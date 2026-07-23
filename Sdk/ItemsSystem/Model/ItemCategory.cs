#if USING_VORTEX_ITEMS
using Vortex.Core.ExtensibleEnumSystem.Abstractions;

namespace Vortex.Sdk.ItemsSystem.Model
{
    /// <summary>
    /// Категория предмета. Базовый класс собственных значений не содержит — набор категорий доменный,
    /// его объявляют пакеты выше через partial-расширение.
    ///
    /// В пресете хранится строковым ключом с выпадающим списком (<c>[ExtEnumKey]</c>), в модели —
    /// разрешённым инстансом. Неразрешимый ключ даёт пустую категорию и ошибку в лог.
    ///
    /// В сохранение не идёт: <see cref="ExtensibleEnum"/> регистрирует собственный конвертер и с точки
    /// зрения сериализатора считается простым типом, поэтому исключение проставлено явно —
    /// иначе сохранённая категория перекрыла бы настройку.
    /// </summary>
    /// <example>
    /// <code>
    /// public partial class ItemCategory
    /// {
    ///     public static readonly ItemCategory Weapon   = new(nameof(Weapon), 100);
    ///     public static readonly ItemCategory Consumable = new(nameof(Consumable), 110);
    /// }
    /// </code>
    /// </example>
    public partial class ItemCategory : ExtensibleEnum
    {
        public ItemCategory(string key, int order) : base(key, order)
        {
        }
    }
}
#endif
