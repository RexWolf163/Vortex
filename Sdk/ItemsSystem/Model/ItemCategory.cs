#if USING_VORTEX_ITEMS
using Vortex.Core.ExtensibleEnumSystem.Abstractions;

namespace Vortex.Sdk.ItemsSystem.Model
{
    /// <summary>
    /// Категория предмета. Доменный набор объявляют пакеты выше через partial-расширение; здесь
    /// есть только <see cref="Unknown"/> — значение по умолчанию, чтобы категория никогда не была
    /// <c>null</c> и потребителю не приходилось проверять её перед сравнением.
    ///
    /// В пресете хранится строковым ключом с выпадающим списком (<c>[ExtEnumKey]</c>), в модели —
    /// разрешённым инстансом. Незаданный ключ даёт <see cref="Unknown"/> молча, неразрешимый —
    /// <see cref="Unknown"/> и ошибку в лог.
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
        /// <summary>
        /// Категория не задана или задана неразрешимым ключом. Порядок 0 — первой в выпадающем
        /// списке инспектора.
        /// </summary>
        public static readonly ItemCategory Unknown = new(nameof(Unknown), 0);

        public ItemCategory(string key, int order) : base(key, order)
        {
        }
    }
}
#endif
