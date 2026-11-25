using System;
using System.Globalization;

namespace Vortex.Core.NetworkSystem
{
    /// <summary>
    /// Форматирование значений параметров адреса и формы: инвариантная культура, дата — ISO 8601, enum — по имени.
    /// Неподдерживаемый тип — ошибка программиста.
    /// </summary>
    internal static class ValueFormatter
    {
        public static string Format(string key, object value)
        {
            switch (value)
            {
                case null:
                    throw new ArgumentNullException(nameof(value), $"[Network] Parameter '{key}' is null.");
                case string s:
                    return s;
                case bool b:
                    return b ? "true" : "false";
                case Enum e:
                    return e.ToString();
                case float f:
                    return f.ToString("R", CultureInfo.InvariantCulture);
                case double d:
                    return d.ToString("R", CultureInfo.InvariantCulture);
                case decimal m:
                    return m.ToString(CultureInfo.InvariantCulture);
                case DateTime dt:
                    return dt.ToString("o", CultureInfo.InvariantCulture);
                case DateTimeOffset dto:
                    return dto.ToString("o", CultureInfo.InvariantCulture);
                case sbyte or byte or short or ushort or int or uint or long or ulong:
                    return ((IFormattable)value).ToString(null, CultureInfo.InvariantCulture);
                default:
                    throw new ArgumentException(
                        $"[Network] Parameter '{key}' has unsupported type {value.GetType().Name}. " +
                        "Supported: string, integer, float, double, decimal, bool, DateTime, DateTimeOffset, enum.");
            }
        }
    }
}
