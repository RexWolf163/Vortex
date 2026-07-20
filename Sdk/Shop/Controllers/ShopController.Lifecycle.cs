using System.Collections.Generic;
using Vortex.Core.DatabaseSystem.Bus;
using Vortex.Sdk.Shop.Model;
using Vortex.Sdk.Shop.Presets;

namespace Vortex.Sdk.Shop.Controllers
{
    public partial class ShopController
    {
        public void Init(ShopSettings settings)
        {
            _settings = settings;
            _initialized = true;
        }

        public void Cleanup()
        {
            _catalog = null;
            _initialized = false;
            ResetIndex();
        }

        /// <summary>
        /// Товар по guid. Каталог — Singleton-записи, грузятся из Database лениво и кешируются.
        /// Неизвестный guid → пустой товар (Inv-5), а не null: система не падает на устаревшей ссылке.
        /// </summary>
        public ShopItemRecord GetItem(string itemGuid)
        {
            EnsureCatalog();
            if (!string.IsNullOrEmpty(itemGuid) && _catalog.TryGetValue(itemGuid, out var item))
                return item;
            return _emptyItem;
        }

        /// <summary>
        /// Ленивая загрузка каталога. Records — Singleton, поэтому GetRecords (без New): экземпляры
        /// уже единственные. Резолв в orchestration-точке, а не скрытым Database-доступом из доменных методов.
        /// </summary>
        private void EnsureCatalog()
        {
            if (_catalog != null)
                return;

            _catalog = new Dictionary<string, ShopItemRecord>();
            foreach (var record in Database.GetRecords<ShopItemRecord>())
                if (record != null && !string.IsNullOrEmpty(record.GuidPreset))
                    _catalog[record.GuidPreset] = record;
        }

        /// <summary>Текущая политика фолбэка. Без инициализации — безопасный дефолт Rollback.</summary>
        private AfterpayMode Mode => _settings != null ? _settings.AfterpayMode : AfterpayMode.Rollback;
    }
}
