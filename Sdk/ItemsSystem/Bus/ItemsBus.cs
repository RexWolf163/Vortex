#if USING_VORTEX_ITEMS
using System;
using System.Collections.Generic;
using System.Linq;
using Vortex.Core.DatabaseSystem.Bus;
using Vortex.Core.Extensions.LogicExtensions.Actions;
using Vortex.Core.LoggerSystem.Bus;
using Vortex.Core.LoggerSystem.Model;
using Vortex.Sdk.ItemsSystem.Model;

namespace Vortex.Sdk.ItemsSystem.Bus
{
    /// <summary>
    /// Шина предметов — статический контроллер с индексом, единственная точка построения предмета
    /// и изменения его состава. Слота сменяемого драйвера не имеет: подменять нечего, логика
    /// платформонезависимая. Понадобится — базу можно сменить аддитивно.
    ///
    /// Владеет осью версии — сквозным монотонным счётчиком, по которому потребители определяют,
    /// что менялось позже. Ось не сохраняется и обнуляется только при запуске приложения: ни новая
    /// игра, ни загрузка её не сбрасывают. Порядок изменений восстанавливается по ней, а не по
    /// часам устройства — часы можно перевести, ось идёт только вперёд.
    ///
    /// Статический класс, а не <c>Singleton</c>: экземпляр никуда не передаётся и интерфейсов не
    /// реализует, а единственное, что база дала бы сверх статики — сброс состояния через Dispose —
    /// прямо противоречит неразрывности оси.
    /// </summary>
    public static class ItemsBus
    {
        /// <summary>
        /// Предмет построен и полностью согласован: состав собран, индекс построен, отметки
        /// проставлены. Срабатывает на обоих путях построения — и при создании из пресета,
        /// и при восстановлении из сохранения, поэтому доменная досборка пишется один раз.
        ///
        /// Подписчики выполняют доменную работу: сверяют состояние с изменившейся настройкой,
        /// дополняют недостающее, вычищают свойства, ставшие ненужными. Событие срабатывает на
        /// каждое построение — при загрузке крупной коллекции это тысячи срабатываний подряд,
        /// поэтому обработчики обязаны быть дешёвыми.
        ///
        /// Изоляция исключений подписчиков не вводится намеренно: исключение в обработчике обрывает
        /// загрузку громко, что предпочтительнее тихо полусобранного предмета.
        /// </summary>
        public static event Action<ItemModel> OnItemCreated;

        private static long _version;

        /// <summary>
        /// Кеш интерфейсов назначения по конкретному классу свойства. Рефлексия на каждое свойство
        /// каждого предмета при построении десятков тысяч экземпляров недопустима.
        /// </summary>
        private static readonly Dictionary<Type, Type[]> PurposeCache = new();

        #region Ось версии

        /// <summary>Текущий конец оси. Читается потребителем перед расчётом производной величины.</summary>
        public static long Version => _version;

        /// <summary>Сдвинуть ось и получить новое значение. Единственная точка инкремента.</summary>
        public static long NextVersion() => ++_version;

        #endregion

        #region Построение

        /// <summary>
        /// Построить предмет по идентификатору пресета, опционально наложив сохранённое состояние.
        ///
        /// Порядок обязателен и потому собран здесь: форма из пресета — наложение состояния —
        /// разрешение категории — построение индекса — отметки — событие. Наложить состояние
        /// не на что, если форма ещё не построена.
        ///
        /// Неразрешимый идентификатор даёт пустую болванку: идентификатор сохранён, состав пуст,
        /// имени, иконки и категории нет. Предмет валиден как объект, но любой запрос свойства даёт
        /// пустой результат. Решение о судьбе такого предмета принимает держатель коллекции.
        /// </summary>
        public static ItemModel Create(string presetGuid, string saveData = null)
        {
            var item = Database.GetNewRecord<ItemModel>(presetGuid);
            if (item == null)
            {
                item = new ItemModel();
                item.MarkUnresolved(presetGuid);
            }

            FillFromPreset(item);

            if (!string.IsNullOrEmpty(saveData))
                item.LoadFromSaveData(saveData);

            RebuildIndex(item);
            StampAll(item);

            OnItemCreated.Fire(item);
            return item;
        }

        /// <summary>
        /// Переносит состав из буфера пресета в словарь по классу. Буфер обнуляется: массивом
        /// модель не владеет, дальше состав живёт только в словарях.
        /// </summary>
        private static void FillFromPreset(ItemModel item)
        {
            var source = item.TakePresetProperties();
            if (source == null)
                return;

            var properties = item.Properties;
            foreach (var property in source)
            {
                if (property == null)
                {
                    Log.Print(LogLevel.Error,
                        $"[Items] Empty property slot in preset. Item: {item.GuidPreset}", item);
                    continue;
                }

                var type = property.GetType();
                if (properties.ContainsKey(type))
                {
                    Log.Print(LogLevel.Error,
                        $"[Items] Duplicated property class «{type.Name}». Item: {item.GuidPreset}", item);
                    continue;
                }

                properties[type] = property;
            }
        }

        /// <summary>
        /// Собирает индекс по интерфейсам назначения заново. Конфликт означает нарушение
        /// уникальности: свойство-нарушитель отклоняется целиком — и из индекса, и из состава,
        /// чтобы в сохранение не уехало недостижимое свойство.
        /// </summary>
        private static void RebuildIndex(ItemModel item)
        {
            var index = item.Index;
            var properties = item.Properties;
            index.Clear();

            List<Type> rejected = null;

            foreach (var pair in properties)
            {
                var purposes = GetPurposeInterfaces(pair.Key);
                var conflict = false;

                foreach (var purpose in purposes)
                {
                    if (!index.ContainsKey(purpose))
                        continue;

                    Log.Print(LogLevel.Error,
                        $"[Items] Purpose «{purpose.Name}» is already taken; property «{pair.Key.Name}» rejected. " +
                        $"Item: {item.GuidPreset}", item);
                    conflict = true;
                    break;
                }

                if (conflict)
                {
                    rejected ??= new List<Type>();
                    rejected.Add(pair.Key);
                    continue;
                }

                foreach (var purpose in purposes)
                    index[purpose] = pair.Value;
            }

            if (rejected == null)
                return;

            foreach (var type in rejected)
                properties.Remove(type);
        }

        /// <summary>
        /// Проставляет отметки предмету и всем его свойствам одним значением оси — «рождены на
        /// версии N». Покрывает и воссозданные из сохранения свойства: у них своей отметки не было.
        /// </summary>
        private static void StampAll(ItemModel item)
        {
            var version = NextVersion();
            item.Stamp(version);

            foreach (var property in item.Properties.Values)
                property.Stamp(version);
        }

        #endregion

        #region Изменение состава

        /// <summary>
        /// Добавить свойство в рантайме. Конфликт по классу или по интерфейсу назначения —
        /// отклонение операции с ошибкой в лог, без исключения: индекс не отравляется.
        /// Проверка идёт до любой мутации, поэтому отказ не оставляет предмет полуизменённым.
        ///
        /// Успех сдвигает отметку предмета: состав изменился.
        /// </summary>
        public static bool AddProperty(ItemModel item, ItemProperty property)
        {
            var type = property.GetType();
            var properties = item.Properties;

            if (properties.ContainsKey(type))
            {
                Log.Print(LogLevel.Error,
                    $"[Items] Property «{type.Name}» already present. Item: {item.GuidPreset}", item);
                return false;
            }

            var index = item.Index;
            var purposes = GetPurposeInterfaces(type);

            foreach (var purpose in purposes)
            {
                if (!index.ContainsKey(purpose))
                    continue;

                Log.Print(LogLevel.Error,
                    $"[Items] Purpose «{purpose.Name}» is already taken; property «{type.Name}» rejected. " +
                    $"Item: {item.GuidPreset}", item);
                return false;
            }

            properties[type] = property;
            foreach (var purpose in purposes)
                index[purpose] = property;

            var version = NextVersion();
            item.Stamp(version);
            property.Stamp(version);
            return true;
        }

        /// <summary>Удалить свойство по интерфейсу назначения или по конкретному классу.</summary>
        public static bool RemoveProperty<T>(ItemModel item) where T : class, IItemProperty =>
            item.GetProperty<T>() is ItemProperty property && RemoveProperty(item, property);

        /// <summary>
        /// Удалить конкретное свойство. Из индекса вычищаются только те назначения, что указывают
        /// именно на него. Успех сдвигает отметку предмета.
        /// </summary>
        public static bool RemoveProperty(ItemModel item, ItemProperty property)
        {
            var type = property.GetType();
            if (!item.Properties.Remove(type))
                return false;

            var index = item.Index;
            foreach (var purpose in GetPurposeInterfaces(type))
                if (index.TryGetValue(purpose, out var present) && ReferenceEquals(present, property))
                    index.Remove(purpose);

            item.Stamp(NextVersion());
            return true;
        }

        #endregion

        #region Интерфейсы назначения

        /// <summary>
        /// Интерфейсы назначения конкретного класса свойства: всё, что унаследовано от маркера
        /// <see cref="IItemProperty"/>, кроме самого маркера. Системные интерфейсы вроде
        /// <c>IComparable</c> в индекс не попадают.
        /// </summary>
        internal static Type[] GetPurposeInterfaces(Type type)
        {
            if (PurposeCache.TryGetValue(type, out var cached))
                return cached;

            var marker = typeof(IItemProperty);
            var purposes = type.GetInterfaces()
                .Where(i => i != marker && marker.IsAssignableFrom(i))
                .ToArray();

            PurposeCache[type] = purposes;
            return purposes;
        }

        #endregion
    }
}
#endif
