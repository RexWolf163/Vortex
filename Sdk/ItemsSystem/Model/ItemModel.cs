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
        ///
        /// Публичность вынужденная — CopyFrom сопоставляет только публичные свойства, и это
        /// единственный канал от пресета к модели. Поэтому переносится буфер, а не рабочий
        /// <see cref="Properties"/>: публичный геттер отдал бы наружу мутабельный состав.
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
        /// Состав по конкретному классу свойства — сериализуемая форма. Геттер непубличный, поэтому
        /// в сохранение попадает только по явной пометке включения.
        ///
        /// <c>internal</c> открывает словарь шине — единственному, кто меняет состав. Ни потребителю
        /// снаружи, ни наследнику модели в чужой сборке он не виден: так менять состав мимо шины
        /// неоткуда, и индекс не может разойтись с составом. Чтение — через методы модели.
        ///
        /// Не <c>private</c>: отбор в сериализацию идёт по конкретному типу, а приватные члены
        /// базового класса рефлексией на наследнике не видны — при наследовании модели private
        /// отвалился бы молча. <c>internal</c> этим свойством обладает, оставаясь закрытым.
        /// </summary>
        [IsPOCO]
        internal Dictionary<Type, ItemProperty> Properties { get; private set; } = new();

        /// <summary>
        /// Индекс по интерфейсам назначения — производная величина. Непубличный и без пометок,
        /// поэтому в сохранение не попадает сам собой: отбор берёт непубличные свойства только
        /// при явном включении. Помечать исключением не требуется.
        /// </summary>
        internal Dictionary<Type, ItemProperty> Index { get; private set; } = new();

        private ItemCategory _category;

        /// <summary>
        /// Разрешённая категория. Никогда не <c>null</c>: незаданный или неразрешимый ключ даёт
        /// <see cref="ItemCategory.Unknown"/>, поэтому сравнивать категорию можно без проверок.
        ///
        /// Разрешается лениво, при первом обращении, и дальше отдаётся из поля — ошибка настройки
        /// попадает в лог один раз, а не на каждое чтение.
        /// </summary>
        public ItemCategory Category => _category ??= ResolveCategory();

        /// <summary>
        /// Предмет изменился — изменилось значение свойства или состав. Инвентарь подписывается на
        /// это у своих предметов и переизлучает как собственное событие, давая реактивность без
        /// полинга. Не сохраняется (событие — поле, не свойство).
        /// </summary>
        public event Action<ItemModel> OnChanged;

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
        /// Сообщить, что предмет изменился — поднимает <see cref="OnChanged"/>. <c>internal</c>:
        /// снаружи предмет дёргают не напрямую, а через <see cref="ItemProperty"/> — его наследник
        /// зовёт защищённый <see cref="ItemProperty.NotifyChanged"/> после изменения своего значения,
        /// передавая владельца. Внутри пакета вызывается ещё при изменении состава свойств.
        /// </summary>
        internal void NotifyChanged() => OnChanged?.Invoke(this);

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

        /// <summary>
        /// Разрешить категорию по ключу. Пустой ключ — штатная ситуация (категория не задана),
        /// непустой неразрешимый — ошибка настройки. Оба случая дают
        /// <see cref="ItemCategory.Unknown"/>, второй — с ошибкой в лог.
        /// </summary>
        private ItemCategory ResolveCategory()
        {
            if (string.IsNullOrEmpty(CategoryKey))
                return ItemCategory.Unknown;

            var category = ExtensibleEnum.GetByKey<ItemCategory>(CategoryKey);
            if (category != null)
                return category;

            Log.Print(LogLevel.Error,
                $"[Items] Category key «{CategoryKey}» is not registered. Item: {GuidPreset}", this);
            return ItemCategory.Unknown;
        }

        #region Bus API

        /// <summary>Забрать буфер переноса из пресета, обнулив его в модели.</summary>
        internal ItemProperty[] TakePresetProperties()
        {
            var source = PresetProperties;
            PresetProperties = null;
            return source;
        }

        /// <summary>
        /// Пометить предмет как собранный по неразрешимому идентификатору пресета: идентификатор
        /// сохранён, настроечных данных нет — ни имени, ни иконки, ни категории. Состав при этом
        /// пуст только в отсутствие сохранения; переданное сохранение восстановит его целиком.
        /// </summary>
        internal void MarkUnresolved(string presetGuid) => GuidPreset = presetGuid;

        #endregion
    }
}
#endif