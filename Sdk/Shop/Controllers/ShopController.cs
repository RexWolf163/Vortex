using System;
using System.Collections.Generic;
using Vortex.Core.System.Abstractions;
using Vortex.Sdk.Shop.Model;
using Vortex.Sdk.Shop.Presets;

namespace Vortex.Sdk.Shop.Controllers
{
    /// <summary>
    /// Центральный контроллер магазина. Один узловой контроллер на пакет; реализует оба доменных
    /// контракта — проводку (<see cref="IShopController"/>) и учёт (<see cref="IShopJournalReader"/>).
    /// Состояние покупок пишет только он (единственная точка эмиссии), логики докладывают исход
    /// вызовом его методов через scoped-контексты.
    ///
    /// Partial-разбиение по темам: Lifecycle (каталог/инициализация), Transaction (поток сделки),
    /// Journal (эмиссия и свёртки), Resolution (перечни и разрешение открытых покупок).
    /// </summary>
    public partial class ShopController : Singleton<ShopController>, IShopController, IShopJournalReader
    {
        private ShopSettings _settings;
        private bool _initialized;

        /// <summary>Кеш каталога: guid → рабочая копия товара. Ленивая загрузка из Database.</summary>
        private Dictionary<string, ShopItemRecord> _catalog;

        /// <summary>Пустой товар — служебная заглушка без логик (Inv-5). Собирается без обращения к журналу.</summary>
        private readonly ShopItemRecord _emptyItem = new();

        public bool IsInitialized => _initialized;

        public event Action<string, PurchaseState> OnPurchaseStateChanged;
        public event Action<ShopPurchase> OnPurchaseClosed;
        public event Action OnJournalUpdated;
    }
}
