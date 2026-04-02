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
        /// Метод для переноса данных из пресета в модель.
        /// Подразумевается копирование конкретного пула данных, ограниченного свойствами
        /// принимающего объекта.
        /// Подразумевается чистое set поле без логики на принимающем объекте
        /// Подразумевается, что иммутабельность должна обеспечиваться в источнике данных
        ///
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
        /// Вложенные ссылочные типы передаются AS-IS.
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