using System.Collections.Generic;

namespace Vortex.Core.Extensions.LogicExtensions
{
    public static class ListExt
    {
        /// <summary>
        /// Проверить на наличие в списке указанного значения
        /// Если значение не найдено - добавить его в список
        /// </summary>
        /// <param name="owner">Список</param>
        /// <param name="data">Вставляемое значение</param>
        /// <typeparam name="T">Тип списка</typeparam>
        public static void AddOnce<T>(this List<T> owner, T data)
        {
            if (owner.Contains(data))
                return;
            owner.Add(data);
        }

        /// <summary>
        /// Получение индекса вхождения в IReadOnlyList
        /// </summary>
        /// <param name="list"></param>
        /// <param name="value"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns>-1 если вхождения не найдено</returns>
        public static int IndexOfItem<T>(this IReadOnlyList<T> list, T value)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (EqualityComparer<T>.Default.Equals(list[i], value))
                    return i;
            }

            return -1;
        }
    }
}