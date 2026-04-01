using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
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
        ///
        /// Вложенные ссылочные типы передаются AS-IS (кроме ICloneable) 
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

                    var value = sourceProp.GetValue(source);
                    if (value is ICloneable cloneable)
                        prop.SetValue(target, cloneable.Clone());
                    else
                        prop.SetValue(target, value.DeepCopy(true));
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
        /// Получить список доступных для чтения параметром объекта
        /// </summary>
        /// <param name="source"></param>
        /// <returns></returns>
        private static PropertyInfo[] GetReadablePropertiesList(this Object source) =>
            source.GetType()
                .GetProperties(BindingFlags.GetProperty | BindingFlags.Public | BindingFlags.Instance)
                .ToArray();


        /*
        public static T DeepCopy<T>(this T obj, bool soft = false)
        {
            if (obj == null) return default;
            return (T)DeepCopyInternal(obj, soft);
        }

        private static object DeepCopyInternal(object obj, bool soft, Dictionary<object, object> visited = null)
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
                    copy.SetValue(DeepCopyInternal(array.GetValue(i), soft, visited), i);

                return copy;
            }

            //Словари
            if (typeof(IDictionary).IsAssignableFrom(type))
            {
                var dict = (IDictionary)obj;
                var copy = (IDictionary)Activator.CreateInstance(type);

                foreach (DictionaryEntry entry in dict)
                {
                    var key = DeepCopyInternal(entry.Key, soft, visited);
                    var value = DeepCopyInternal(entry.Value, soft, visited);
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
                    copy.Add(DeepCopyInternal(element, soft, visited));

                return copy;
            }

            // Коллекции
            if (typeof(IEnumerable).IsAssignableFrom(type))
            {
                var ar = (IEnumerable)obj;
                var copy = Activator.CreateInstance(type);
                foreach (var element in ar)
                    AddToCollection(copy, DeepCopyInternal(element, soft, visited));

                return copy;
            }

            // Объекты
            visited ??= new Dictionary<object, object>(ReferenceEqualityComparer.Instance);

            // Если объект уже копировался, возвращаем ссылку на существующую копию
            // Это разрывает цикл и сохраняет структуру графа
            if (visited.TryGetValue(obj, out var existingCopy))
                return existingCopy;

            try
            {
                if (obj is ICloneable cloneable)
                    return cloneable.Clone();

                var copyObj = Activator.CreateInstance(type);
                visited.AddNew(obj, copyObj);
                var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

                // Копирование полей
                foreach (var field in type.GetFields(flags))
                {
                    var value = field.GetValue(obj);
                    field.SetValue(copyObj, DeepCopyInternal(value, soft, visited));
                }

                return copyObj;
            }
            catch (Exception e)
            {
                if (soft)
                {
                    Log.Print(LogLevel.Common, $"DeepCopy failed for {type.Name}: {e.Message}. Pointer was return.",
                        obj);
                    return obj;
                }

                Log.Print(LogLevel.Error, $"DeepCopy failed for {type.Name}: {e.Message}", obj);
                return null;
            }
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
*/
    }

    public sealed class ReferenceEqualityComparer : IEqualityComparer<object>
    {
        public static readonly ReferenceEqualityComparer Instance = new ReferenceEqualityComparer();

        public new bool Equals(object x, object y)
        {
            return ReferenceEquals(x, y);
        }

        public int GetHashCode(object obj)
        {
            return RuntimeHelpers.GetHashCode(obj);
        }
    }
}