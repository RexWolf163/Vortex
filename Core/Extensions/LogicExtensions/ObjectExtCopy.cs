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
                foreach (var property in properties)
                {
                    var prop = modelType.GetProperty(property.Name);
                    if (prop == null || !prop.CanWrite)
                        continue;

                    prop.SetValue(target, property.GetValue(source));
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
    }
}