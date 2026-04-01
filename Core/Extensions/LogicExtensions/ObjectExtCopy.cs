using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Vortex.Core.LoggerSystem.Bus;
using Vortex.Core.LoggerSystem.Model;

namespace Vortex.Core.Extensions.LogicExtensions
{
    public static class ObjectExtCopy
    {
        /// <summary>
        /// Заполняет свои свойства имеющимися в объекте данными
        /// Рассматривает только поля в source которые отвечают флагам
        /// BindingFlags.GetProperty | BindingFlags.Public | BindingFlags.Instance
        ///
        /// Чтобы провести запись в другой объект, свойство у этого объекта должно быть CanWrite
        ///
        /// Можно записывать не однотипные объекты друг в друга.
        /// 
        /// При несовпадении свойств исключения в нормальном случае не выбрасывается 
        /// </summary>
        /// <param name="target"></param>
        /// <param name="source"></param>
        /// <returns>TRUE при удачном результате записи</returns>
        public static bool CopyFrom(this Object target, Object source)
        {
            try
            {
                var properties = source.GetReadablePropertiesList();
                var modelType = target.GetType();
                var targetProperties = modelType.GetProperties()
                    .Where(p => p.CanWrite)
                    .ToDictionary(p => p.Name, p => p, StringComparer.OrdinalIgnoreCase);

                foreach (var sourceProp in properties)
                {
                    if (!targetProperties.TryGetValue(sourceProp.Name, out var prop))
                        continue;
                    if (prop == null || !prop.CanWrite)
                        continue;
                    // Пропускаем индексированные свойства
                    if (prop.GetIndexParameters().Length > 0) continue;

                    if (!prop.PropertyType.IsAssignableFrom(sourceProp.PropertyType))
                    {
                        Log.Print(LogLevel.Error, "Критичное несовпадение структуры объектов", target);
                        return false;
                    }

                    prop.SetValue(target, sourceProp.CopyValue(source));
                }

                return true;
            }
            catch (Exception e)
            {
                Log.Print(LogLevel.Error, e.Message, target);
                return false;
            }
        }

        private static object CopyValue(this PropertyInfo property, object source)
        {
            var sourceValue = property.GetValue(source);
            // null копируем как есть
            if (sourceValue == null)
                return null;

            var sourceType = sourceValue.GetType();
            // Примитивы, строки, enum, DateTime и т.д. — копируем напрямую
            if (IsSimpleType(sourceType))
                return sourceValue;

            // Обычный сложный объект
            return sourceValue.DeepCopy();
        }

        /// <summary>
        /// Проверка на простой тип не требующий глубокого копирования
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        private static bool IsSimpleType(Type type)
        {
            return type.IsPrimitive
                   || type.IsEnum
                   || type == typeof(string)
                   || type == typeof(DateTime)
                   || type == typeof(decimal)
                   || type == typeof(Guid)
                   || type == typeof(DateTimeOffset);
        }

        /// <summary>
        /// Получить список доступных для чтения параметром объекта
        /// </summary>
        /// <param name="source"></param>
        /// <returns></returns>
        private static PropertyInfo[] GetReadablePropertiesList(this Object source) =>
            source.GetType()
                .GetProperties(BindingFlags.GetProperty | BindingFlags.Public | BindingFlags.Instance)
                .ToArray();


        public static T DeepCopy<T>(this T obj)
        {
            if (obj == null) return default;
            return (T)DeepCopyInternal(obj);
        }

        private static object DeepCopyInternal(object obj, Dictionary<object, object> visited = null)
        {
            if (obj == null) return null;

            var type = obj.GetType();

            // Примитивные типы, строки, перечисления, DateTime, Decimal, Guid
            if (type.IsPrimitive || type == typeof(string) || type.IsEnum ||
                type == typeof(DateTime) || type == typeof(decimal) || type == typeof(Guid))
            {
                return obj;
            }

            // Массивы
            if (type.IsArray)
            {
                var array = (Array)obj;
                var elementType = type.GetElementType();
                var copy = Array.CreateInstance(elementType, array.Length);
                for (var i = 0; i < array.Length; i++)
                    copy.SetValue(DeepCopyInternal(array.GetValue(i), visited), i);

                return copy;
            }

            //Словари
            if (typeof(IDictionary).IsAssignableFrom(type))
            {
                var dict = (IDictionary)obj;
                var copy = (IDictionary)Activator.CreateInstance(type);

                foreach (DictionaryEntry entry in dict)
                {
                    var key = DeepCopyInternal(entry.Key, visited);
                    var value = DeepCopyInternal(entry.Value, visited);
                    copy.Add(key, value);
                }

                return copy;
            }

            //Другие списки
            if (typeof(IList).IsAssignableFrom(type))
            {
                var list = (IList)obj;
                var copy = (IList)Activator.CreateInstance(type);

                foreach (var element in list)
                    copy.Add(DeepCopyInternal(element, visited));

                return copy;
            }

            // Коллекции
            if (typeof(IEnumerable).IsAssignableFrom(type))
            {
                var ar = (IEnumerable)obj;
                var copy = Activator.CreateInstance(type);
                foreach (var element in ar)
                    AddToCollection(copy, DeepCopyInternal(element, visited));

                return copy;
            }

            // Объекты
            visited ??= new Dictionary<object, object>();

            // Если объект уже копировался, возвращаем ссылку на существующую копию
            // Это разрывает цикл и сохраняет структуру графа
            if (visited.TryGetValue(obj, out var existingCopy))
                return existingCopy;

            try
            {
                var copyObj = Activator.CreateInstance(type);
                visited.AddNew(obj, copyObj);
                var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

                // Копирование полей
                foreach (var field in type.GetFields(flags))
                {
                    var value = field.GetValue(obj);
                    field.SetValue(copyObj, DeepCopyInternal(value, visited));
                }

                return copyObj;
            }
            catch(Exception e)
            {
                Log.Print(LogLevel.Error, $"DeepCopy failed for {type.Name}: {e.Message}", obj);
                return null;
            }

            /*
            // Копирование свойств
            foreach (var prop in type.GetProperties(flags))
            {
                if (prop.CanRead && prop.CanWrite && prop.GetIndexParameters().Length == 0)
                {
                    var value = prop.GetValue(obj);
                    prop.SetValue(copyObj, DeepCopyInternal(value, visited));
                }
            }
            */
        }

        /// <summary>
        /// Попытка заполнить незнакомую коллекцию
        /// </summary>
        /// <param name="collection"></param>
        /// <param name="item"></param>
        private static void AddToCollection(object collection, object item)
        {
            // 1. Пробуем быстрый путь через non-generic IList
            if (collection is IList nonGenericList)
            {
                nonGenericList.Add(item);
                return;
            }

            // 2. Пробуем быстрый путь через generic IList<T>
            var collectionType = collection.GetType();
            foreach (var iface in collectionType.GetInterfaces())
            {
                if (!iface.IsGenericType || iface.GetGenericTypeDefinition() != typeof(IList<>)) continue;
                var addMethod = iface.GetMethod("Add");
                addMethod?.Invoke(collection, new[] { item });
                return;
            }

            // 3. Фоллбэк: ищем любой публичный метод Add через рефлексию
            // (для коллекций типа Stack<T>.Push, Queue<T>.Enqueue это не сработает, но для большинства стандартных - да)
            var add = collectionType.GetMethod("Add", BindingFlags.Public | BindingFlags.Instance);
            if (add == null) return;
            var param = add.GetParameters()[0];
            var value = param.ParameterType.IsAssignableFrom(item?.GetType() ?? typeof(object))
                ? item
                : Convert.ChangeType(item, param.ParameterType);
            add.Invoke(collection, new[] { value });
        }
    }
}