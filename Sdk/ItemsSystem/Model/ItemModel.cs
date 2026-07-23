#if USING_VORTEX_ITEMS
using System;
using System.Collections.Generic;
using Vortex.Core.DatabaseSystem.Model;
using Vortex.Core.Extensions.LogicExtensions.SerializationSystem;
using Vortex.Core.ExtensibleEnumSystem.Abstractions;
using Vortex.Core.LoggerSystem.Bus;
using Vortex.Core.LoggerSystem.Model;
using Vortex.Sdk.ItemsSystem.Bus;

namespace Vortex.Sdk.ItemsSystem.Model
{
    /// <summary>
    /// Предмет — многоэкземплярная запись БД с набором полиморфных свойств. Собирается только
    /// через <see cref="ItemsBus"/>: обход шины означает предмет без индекса и без отметок.
    ///
    /// Свойства лежат в двух непубличных словарях. <see cref="Properties"/> — по конкретному классу,
    /// сериализуемый: словарь даёт слияние при загрузке штатными средствами (список при загрузке
    /// перетирается целиком) и хранит каждое свойство ровно один раз, поэтому сериализатор не
    /// встречает один объект дважды. <see cref="Index"/> — по интерфейсам назначения, не
    /// сериализуется, пересобирается при построении и при изменении состава.
    ///
    /// Из такого хранения следует главный инвариант: не более одного свойства на интерфейс
    /// назначения. Он структурен — два свойства на одном интерфейсе физически не помещаются
    /// в индекс, конфликт вылезает при вставке.
    ///
    /// Цена — память. На 10 000 предметов подсистема занимает порядка 16–20 МБ против 12 МБ
    /// у варианта с одним словарём и перебором. Размен сознательный: на фоне типичной сборки,
    /// где текстуры измеряются гигабайтами, это доли процента бюджета.
    /// </summary>
    [Serializable, POCO]
    public class ItemModel : Record
    {
        /// <summary>
        /// Буфер переноса состава из пресета. Заполняется CopyFrom при построении записи
        /// (глубокая копия — каждый предмет получает собственные независимые свойства),
        /// сразу же расходуется шиной в словари и обнуляется. Массивом модель не владеет:
        /// после построения здесь всегда <c>null</c>.
        /// </summary>
        [NotPOCO]
        public ItemProperty[] PresetProperties { get; protected set; }

        /// <summary>
        /// Ключ категории из пресета. Приходит настройкой при каждом построении, поэтому
        /// в сохранение не идёт. Разрешённое значение — <see cref="Category"/>.
        /// </summary>
        [NotPOCO]
        public string CategoryKey { get; protected set; }

        /// <summary>
        /// Состав по конкретному классу свойства — сериализуемая форма. Непубличный с явной
        /// пометкой включения: состав меняется только через шину, снаружи доступно чтение
        /// через методы модели.
        ///
        /// Уровень доступа protected, а не private: отбор в сериализацию идёт по конкретному типу,
        /// а приватные члены базового класса рефлексией на наследнике не видны — при наследовании
        /// модели private отвалился бы молча.
        /// </summary>
        [IsPOCO]
        protected Dictionary<Type, ItemProperty> Properties { get; set; } = new();

        /// <summary>
        /// Индекс по интерфейсам назначения — производная величина. Непубличный и без пометок,
        /// поэтому в сохранение не попадает сам собой: отбор берёт непубличные свойства только
        /// при явном включении. Помечать исключением не требуется.
        /// </summary>
        protected Dictionary<Type, ItemProperty> Index { get; set; } = new();

        private ItemCategory _category;
        private long _version;

        /// <summary>Разрешённая категория. <c>null</c>, если ключ пуст или не зарегистрирован.</summary>
        public ItemCategory Category => _category;

        /// <summary>
        /// Отметка по оси версии — момент последнего изменения состава предмета. Изменение значения
        /// отдельного свойства её не двигает: у свойства своя отметка. Не сохраняется — свойство
        /// только для чтения, сериализатор берёт лишь свойства с сеттером.
        /// </summary>
        public long Version => _version;

        /// <summary>Все свойства предмета для перебора. Изменение состава — только через шину.</summary>
        public IReadOnlyCollection<ItemProperty> AllProperties => Properties.Values;

        /// <summary>
        /// Свойство по интерфейсу назначения — обращение к индексу, O(1). Допускается и запрос
        /// по конкретному классу: при промахе по индексу проверяется словарь состава.
        /// Отсутствие — <c>null</c>, обработка на вызывающем.
        /// </summary>
        public T GetProperty<T>() where T : class, IItemProperty =>
            Index.TryGetValue(typeof(T), out var byPurpose)
                ? byPurpose as T
                : Properties.TryGetValue(typeof(T), out var byClass)
                    ? byClass as T
                    : null;

        /// <summary>Есть ли у предмета свойство указанного назначения.</summary>
        public bool HasProperty<T>() where T : class, IItemProperty =>
            Index.ContainsKey(typeof(T)) || Properties.ContainsKey(typeof(T));

        /// <summary>
        /// Отметить предмет изменённым — сдвинуть его отметку на текущий конец оси. Шина делает это
        /// сама при изменении состава; метод нужен доменной логике, меняющей предмет способом,
        /// о котором пакет не знает.
        /// </summary>
        public void Touch() => _version = ItemsBus.NextVersion();

        /// <summary>Сохраняется игровое состояние: состав свойств и их изменяемые значения.</summary>
        public override string GetDataForSave() => this.SerializeProperties();

        /// <summary>
        /// Накладывает сохранённое состояние на уже построенную из пресета форму. Именно наложение,
        /// а не пересоздание: словарь сливается по ключам — свойство из пресета получает сохранённые
        /// значения, свойство без пары в сохранении остаётся в исходном состоянии, свойство из
        /// сохранения без пары в пресете воссоздаётся.
        ///
        /// Вызывается шиной. Прямой вызов оставит индекс и отметки несогласованными с составом.
        /// </summary>
        public override void LoadFromSaveData(string data) => data.UploadProperties(this);

        #region Bus API

        /// <summary>Состав по классу — рабочая поверхность шины.</summary>
        internal Dictionary<Type, ItemProperty> PropertiesMap => Properties;

        /// <summary>Индекс по интерфейсам — рабочая поверхность шины.</summary>
        internal Dictionary<Type, ItemProperty> IndexMap => Index;

        /// <summary>Забрать буфер переноса из пресета, обнулив его в модели.</summary>
        internal ItemProperty[] TakePresetProperties()
        {
            var source = PresetProperties;
            PresetProperties = null;
            return source;
        }

        /// <summary>Проставить отметку конкретным значением.</summary>
        internal void Stamp(long version) => _version = version;

        /// <summary>
        /// Разрешить категорию по ключу. Пустой ключ — штатная ситуация (категория не задана),
        /// непустой неразрешимый — ошибка настройки.
        /// </summary>
        internal void ResolveCategory()
        {
            if (string.IsNullOrEmpty(CategoryKey))
            {
                _category = null;
                return;
            }

            _category = ExtensibleEnum.GetByKey<ItemCategory>(CategoryKey);
            if (_category == null)
                Log.Print(LogLevel.Error,
                    $"[Items] Category key «{CategoryKey}» is not registered. Item: {GuidPreset}", this);
        }

        /// <summary>
        /// Пометить предмет как собранный по неразрешимому идентификатору пресета: идентификатор
        /// сохранён, состав пуст, имени, иконки и категории нет.
        /// </summary>
        internal void MarkUnresolved(string presetGuid) => GuidPreset = presetGuid;

        #endregion
    }
}
#endif
