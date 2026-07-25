#if USING_VORTEX_ITEMS
using System;
using System.Collections.Generic;
using UnityEngine;
using Vortex.Core.Extensions.LogicExtensions;
using Vortex.Core.Extensions.LogicExtensions.Actions;
using Vortex.Core.Extensions.LogicExtensions.SerializationSystem;
using Vortex.Core.LoggerSystem.Bus;
using Vortex.Core.LoggerSystem.Model;
using Vortex.Sdk.InventorySystem.Bus;
using Vortex.Sdk.InventorySystem.Controllers;
using Vortex.Sdk.InventorySystem.Verifiers;
using Vortex.Sdk.ItemsSystem.Bus;
using Vortex.Sdk.ItemsSystem.Model;

namespace Vortex.Sdk.InventorySystem.Model
{
    /// <summary>
    /// Инвентарь — встраиваемая коллекция предметов с набором ограничивающих правил. Не запись БД:
    /// объявляется полем в настройке хозяина (<c>[SerializeField] private Inventory ...</c>) и
    /// переезжает в его модель глубокой копией вместе с настроенными верификаторами. Живёт и умирает
    /// с моделью хозяина.
    ///
    /// Состав хранится живыми <see cref="ItemModel"/>, но в сохранение уезжают не они, а список пар
    /// «идентификатор пресета и состояние», упакованный в одну строку (<see cref="SavedState"/>).
    /// Живые предметы отдавать сериализатору нельзя: он построил бы их в обход шины — без индекса,
    /// без отметок, без события. Восстановление идёт лениво, через шину предметов, при первом
    /// обращении к составу.
    ///
    /// Верификаторы, политика пачек и стартовое наполнение — настройка. Они хранятся полями, а не
    /// свойствами, поэтому сериализатор их не видит и в сохранение они не идут: при каждом построении
    /// приходят из настройки заново, и правка баланса доходит до старых сохранений.
    /// </summary>
    [Serializable, POCO]
    public class Inventory
    {
        [SerializeReference] private List<InventoryVerifier> verifiers = new();
        [SerializeField] private StackPolicy policy = StackPolicy.Merge;
        [SerializeField] private List<StartupEntry> startupFill = new();

        private List<ItemModel> _items;
        private string _rawState;
        private bool _materialized;
        private bool _corrupt;
        private bool _disposed;

        /// <summary>Набор верификаторов. Пустой означает «влезает всё».</summary>
        public IReadOnlyList<InventoryVerifier> Verifiers => verifiers;

        /// <summary>Политика складывания в пачки.</summary>
        public StackPolicy Policy => policy;

        /// <summary>
        /// В состав вошёл новый экземпляр предмета. Для реактивного UI без полинга. Материализация
        /// (первичная загрузка/стартовое наполнение) события не поднимает — это начальный снимок,
        /// его потребитель читает через <see cref="Items"/>, а на события подписывается для дельт.
        /// Не сериализуется (событие — поле, не свойство).
        /// </summary>
        public event Action<ItemModel> OnItemAdded;

        /// <summary>Экземпляр покинул состав (изъятие или уничтожение).</summary>
        public event Action<ItemModel> OnItemRemoved;

        /// <summary>
        /// У предмета в составе изменилось содержимое — количество в пачке, прочность, тающий вес,
        /// добавленное/убранное свойство. Инвентарь подписан на <see cref="ItemModel.OnChanged"/>
        /// своих предметов и переизлучает его сюда, поэтому изменение значения доходит без полинга,
        /// даже когда его сделал внешний контроллер, не знающий про инвентарь.
        /// </summary>
        public event Action<ItemModel> OnItemChanged;

        /// <summary>
        /// Состав для чтения и перебора. Первое обращение материализует инвентарь: строит предметы
        /// из сохранения либо из стартового наполнения. Изменять состав — только через
        /// <see cref="InventoryController"/>.
        /// </summary>
        public IReadOnlyList<ItemModel> Items
        {
            get
            {
                Materialize();
                return _items;
            }
        }

        /// <summary>
        /// Упакованное состояние состава — единственное, что уходит в сохранение. Геттер собирает
        /// пары из живого состава и жмёт их в base64 одним куском на весь инвентарь: поштучная
        /// упаковка раздувала бы сейв служебным заголовком, а нарезка на предметы — экранированием
        /// при вложенности. Пока инвентарь не материализован, отдаётся уже загруженная строка как есть.
        ///
        /// Сеттер сбрасывает материализацию: что бы ни пришло при загрузке, оно побеждает возможную
        /// преждевременную сборку из стартового наполнения. Новая игра сеттер не вызывает —
        /// стартовое наполнение остаётся в силе.
        /// </summary>
        [IsPOCO]
        private string SavedState
        {
            get => _materialized && !_corrupt ? Pack() : _rawState;
            set
            {
                _rawState = value;
                _materialized = false;
                _corrupt = false;
                _items = null;
            }
        }

        private string Pack()
        {
            var list = new List<SavedItem>(_items.Count);
            foreach (var item in _items)
                list.Add(new SavedItem { Preset = item.GuidPreset, State = item.GetDataForSave() });
            return list.SerializeProperties().Compress();
        }

        private void Materialize()
        {
            if (_materialized)
                return;

            _items = new List<ItemModel>();
            _materialized = true;

            if (_rawState != null)
                BuildFromSave();
            else
                BuildFromStartup();

            // Битую строку не затираем: иначе первый автосейв перезапишет ячейку пустой и потеряет
            // содержимое безвозвратно. Сохранённая строка отдаётся обратно в сейв как есть — шанс на
            // ручное восстановление или миграцию остаётся.
            if (!_corrupt)
                _rawState = null;

            InventoryRegistry.Register(this);
        }

        private void BuildFromSave()
        {
            List<SavedItem> saved;
            try
            {
                saved = _rawState.Decompress().DeserializeProperties<List<SavedItem>>();
            }
            catch (Exception e)
            {
                _corrupt = true;
                Log.Print(LogLevel.Error,
                    $"[Inventory] Corrupted saved composition, inventory left empty: {e.Message}", null);
                return;
            }

            if (saved == null)
            {
                _corrupt = true;
                Log.Print(LogLevel.Error,
                    "[Inventory] Saved composition failed to parse, inventory left empty", null);
                return;
            }

            foreach (var entry in saved)
            {
                var item = ItemsBus.Create(entry.Preset, entry.State);
                _items.Add(item);
                Attach(item);
            }
        }

        private void BuildFromStartup()
        {
            foreach (var entry in startupFill)
            {
                var item = ItemsBus.Create(entry.PresetGuid);
                StackController.ApplyStartupCount(item, entry.Count);
                _items.Add(item);
                Attach(item);
            }
        }

        #region Controller API

        /// <summary>Живой состав под изменение. Материализует инвентарь при первом обращении.</summary>
        internal List<ItemModel> Composition
        {
            get
            {
                Materialize();
                return _items;
            }
        }

        /// <summary>
        /// Подписка на сигнал изменения предмета — вешается при входе предмета в состав (и при
        /// материализации). Так изменение значения свойства (количество, распад веса) переизлучается
        /// как <see cref="OnItemChanged"/>. Отписка — <see cref="Detach"/> при выходе из состава.
        /// </summary>
        internal void Attach(ItemModel item) => item.OnChanged += HandleItemChanged;

        /// <inheritdoc cref="Attach"/>
        internal void Detach(ItemModel item) => item.OnChanged -= HandleItemChanged;

        private void HandleItemChanged(ItemModel item) => OnItemChanged.Fire(item);

        /// <summary>Предмет вошёл в состав: подписаться и объявить. Зовёт контроллер.</summary>
        internal void RaiseAdded(ItemModel item)
        {
            Attach(item);
            OnItemAdded.Fire(item);
        }

        /// <summary>Предмет покинул состав: отписаться и объявить. Зовёт контроллер.</summary>
        internal void RaiseRemoved(ItemModel item)
        {
            Detach(item);
            OnItemRemoved.Fire(item);
        }

        #endregion

        /// <summary>
        /// Уничтожение инвентаря хозяином: содержимое удаляется рекурсивно, вместе с содержимым
        /// вложенных контейнеров, и только после этого публикуется событие. Порядок такой, потому что
        /// событие — для уборки, а не для подбора: кому содержимое нужно, забирает его заранее.
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;

            Materialize();
            InventoryController.PurgeRecursive(this);
            InventoryRegistry.Unregister(this);
            InventoryBus.NotifyDestroyed(this);
        }
    }
}
#endif
