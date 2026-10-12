using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Vortex.Core.LoggerSystem.Bus;
using Vortex.Core.LoggerSystem.Model;
using Object = System.Object;

namespace Vortex.Core.Extensions.LogicExtensions
{
    public static class ObjectExtCopy
    {
        // Кеши рефлексии по типу. CopyFrom вызывается часто: на загрузке Database (каждый
        // Singleton-пресет) и в рантайме (каждый GetNewRecord для MultiInstance). Без кеша
        // GetProperties + LINQ гоняются заново на каждый вызов. Ключ — Type, плоский Dictionary
        // (вызовы идут из main-потока, как и FieldsCache в ObjectExtDeepClone).
        private static readonly Dictionary<Type, PropertyInfo[]> ReadablePropertiesCache = new();
        private static readonly Dictionary<Type, Dictionary<string, PropertyInfo>> WritablePropertiesCache = new();

        // Типы-исключения из CopyExclusionMarker (типы его статических полей). Собираются один раз.
        private static Type[] _excludedDeclaringTypes;

        /// <summary>
        /// Создает глубокую копию объекта через рефлексию.
        /// 
        /// Порядок обработки типов:
        /// 1. null → default(T)
        /// 2. Примитивы, string, decimal, DateTime, DateTimeOffset, TimeSpan, Guid, Uri, Version, enum → возврат as-is
        /// 3. Платформенные примитивы (типы, отмеченные в SimpleTypeMarker платформенным слоем) → возврат по ссылке
        /// 4. Циклические ссылки → возврат ранее созданной копии из visited
        /// 5. Array → поэлементное рекурсивное копирование
        /// 6. IDictionary → ключи as-is, значения рекурсивно
        /// 7. IList → рекурсивное копирование элементов
        /// 8. ICloneable → Clone(). Контракт: реализация Clone() ДОЛЖНА выполнять deep copy.
        ///    Если Clone() делает shallow copy — вложенные ссылки будут разделяться с оригиналом
        /// 9. Прочие объекты → Activator.CreateInstance (fallback: FormatterServices.GetUninitializedObject)
        ///    + копирование всех полей (включая private и наследованные)
        /// 
        /// Граничные случаи:
        /// - Создание экземпляра: Activator → FormatterServices fallback; оба провалились → returnOriginalOnError
        /// - returnOriginalOnError подмешивает оригинал в граф копии — мутации оригинала будут видны
        /// - readonly поля копируются через рефлексию (SetValue обходит readonly)
        /// - FieldInfo[] (в DeepCopy) и PropertyInfo (readable source + writable target в CopyFrom)
        ///   кешируются статически по типу, не очищаются
        /// </summary>
        /// <param name="target">Целевой объект</param>
        /// <param name="source">Исходный объект</param>
        /// <returns>Глубокая копия или default при null</returns>
        public static bool CopyFrom(this Object target, Object source)
        {
            try
            {
                var properties = source.GetReadablePropertiesList();
                var targetProperties = target.GetType().GetWritablePropertiesMap();

                foreach (var sourceProp in properties)
                {
                    // Карта target-свойств уже отфильтрована: только CanWrite и без индексаторов.
                    if (!targetProperties.TryGetValue(sourceProp.Name, out var prop))
                        continue;

                    var value = sourceProp.GetValue(source);
                    // Массив исключён из ветки ICloneable намеренно: Array.Clone() — поверхностная
                    // копия (новый массив, но элементы те же по ссылке), из-за чего все записи,
                    // построенные из одного пресета, делили бы один экземпляр каждого элемента.
                    // DeepCopy копирует массив поэлементно рекурсивно, давая независимые элементы;
                    // не-массивные ICloneable по-прежнему копируются своим Clone().
                    if (value is ICloneable cloneable && value is not Array)
                        prop.SetValue(target, cloneable.Clone());
                    else
                        prop.SetValue(target, value.DeepCopy());
                }

                return true;
            }
            catch (Exception e)
            {
                Log.Print(LogLevel.Error, e.Message, target);
                return false;
            }
        }

        /// <summary>
        /// Список доступных для чтения свойств объекта (кешируется по типу).
        /// Исключаются свойства, объявленные в типах из <see cref="CopyExclusionMarker"/> и их предках —
        /// копируем только доменные поля пресета, не служебные свойства платформенного базового класса.
        /// </summary>
        private static PropertyInfo[] GetReadablePropertiesList(this Object source)
        {
            var type = source.GetType();
            if (ReadablePropertiesCache.TryGetValue(type, out var cached))
                return cached;

            var props = type
                .GetProperties(BindingFlags.GetProperty | BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.DeclaringType != null && !IsExcludedDeclaringType(p.DeclaringType))
                .ToArray();

            ReadablePropertiesCache[type] = props;
            return props;
        }

        /// <summary>
        /// Объявлено ли свойство в типе-исключении из <see cref="CopyExclusionMarker"/> или в его предке
        /// (тип-исключение приводим к <paramref name="declaringType"/>).
        /// </summary>
        private static bool IsExcludedDeclaringType(Type declaringType)
        {
            _excludedDeclaringTypes ??= typeof(CopyExclusionMarker)
                .GetFields(BindingFlags.Public | BindingFlags.Static)
                .Select(f => f.FieldType)
                .ToArray();

            for (var i = 0; i < _excludedDeclaringTypes.Length; i++)
                if (declaringType.IsAssignableFrom(_excludedDeclaringTypes[i]))
                    return true;

            return false;
        }

        /// <summary>
        /// Карта «имя свойства → PropertyInfo» для всех записываемых свойств типа
        /// (кешируется по типу). Имена сравниваются без учёта регистра — как в исходной версии.
        /// </summary>
        private static Dictionary<string, PropertyInfo> GetWritablePropertiesMap(this Type type)
        {
            if (WritablePropertiesCache.TryGetValue(type, out var cached))
                return cached;

            var map = type.GetProperties()
                .Where(p => p.CanWrite && p.GetIndexParameters().Length == 0)
                .ToDictionary(p => p.Name, p => p, StringComparer.OrdinalIgnoreCase);

            WritablePropertiesCache[type] = map;
            return map;
        }
    }
}