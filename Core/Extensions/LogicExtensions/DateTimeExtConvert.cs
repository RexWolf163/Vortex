using System;

namespace Vortex.Core.Extensions.LogicExtensions
{
    public static class DateTimeExtConvert
    {
        /// <summary>
        /// Преобразование DateTime в число
        /// </summary>
        /// <param name="date"></param>
        /// <returns></returns>
        public static long ToUnixTime(this DateTime date)
        {
            var epochStart = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            return Convert.ToInt64((date - epochStart).TotalMilliseconds);
        }

        /// <summary>
        /// Преобразование числа в DateTime
        /// </summary>
        /// <param name="date"></param>
        /// <param name="millisecondsSinceEpoch"></param>
        /// <returns></returns>
        public static DateTime FromUnixTime(this DateTime date, long millisecondsSinceEpoch)
        {
            date = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            return date.AddMilliseconds(millisecondsSinceEpoch);
        }
    }
}