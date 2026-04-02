using System;
using System.Linq;
using System.Reflection;
using Vortex.Core.LoggerSystem.Bus;
using Vortex.Core.LoggerSystem.Model;

namespace Vortex.Core.Extensions.LogicExtensions
{
    public static class ObjectExtCopy
    {
        /// <summary>
        /// Создает глубокую копию объекта через рефлексию.
        ///
        /// Порядок обработки типов:
        /// 1. null → default(T)
        /// 2. Примитивы, string, decimal, DateTime, DateTimeOffset, TimeSpan, Guid, Uri, Version, enum → возврат as-is
        /// 3. Платформенные примитивы (SimpleTypeMarker: Sprite, GameObject и т.д.) → возврат по ссылке
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
        /// - FieldInfo[] кешируется статически по типу, не очищается
        /// </summary>
        /// <param name="source">Исходный объект</param>
        /// <param name="returnOriginalOnError">При ошибке создания экземпляра вернуть оригинал вместо null</param>
        /// <typeparam name="T">Тип объекта</typeparam>
        /// <returns>Глубокая копия или default при null</returns>
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
                        prop.SetValue(target, value.DeepCopy());
                }
                /*foreach (var sourceProp in properties)
                {
                    if (!targetProperties.TryGetValue(sourceProp.Name, out var prop))
                        continue;
                    if (prop == null || !prop.CanWrite)
                        continue;

                    var value = sourceProp.GetValue(source);
                    prop.SetValue(target, value);
                }*/

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
    }
}