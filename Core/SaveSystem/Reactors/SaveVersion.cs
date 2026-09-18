using System;

namespace Vortex.Core.SaveSystem.Reactors
{
    /// <summary>
    /// Сравнение версий сейва: строки вида <c>1.0.260910</c>, произвольное число сегментов через точку.
    /// Сегменты сравниваются как числа слева направо; недостающий сегмент считается нулём, поэтому
    /// <c>1.0</c> и <c>1.0.0</c> равны. Из сегмента берётся числовой префикс до первого нецифрового символа
    /// (<c>260910-rc1</c> → 260910), остаток игнорируется: суффиксы сборок на порядок версий не влияют.
    /// Пустая версия — самая старая: сейвы записаны до появления поля в сводке.
    /// </summary>
    public static class SaveVersion
    {
        /// <summary>Отрицательное — <paramref name="left"/> старше, 0 — равны, положительное — новее.</summary>
        public static int Compare(string left, string right)
        {
            var leftSegments = Split(left);
            var rightSegments = Split(right);
            var count = Math.Max(leftSegments.Length, rightSegments.Length);

            for (var i = 0; i < count; i++)
            {
                var result = SegmentValue(leftSegments, i).CompareTo(SegmentValue(rightSegments, i));
                if (result != 0)
                    return result;
            }

            return 0;
        }

        /// <summary><paramref name="version"/> не старше <paramref name="other"/>.</summary>
        public static bool IsAtLeast(string version, string other) => Compare(version, other) >= 0;

        /// <summary><paramref name="version"/> не новее <paramref name="other"/>.</summary>
        public static bool IsAtMost(string version, string other) => Compare(version, other) <= 0;

        private static string[] Split(string version) =>
            string.IsNullOrWhiteSpace(version) ? new string[0] : version.Split('.');

        private static long SegmentValue(string[] segments, int index)
        {
            if (index >= segments.Length)
                return 0;

            var segment = segments[index];
            long value = 0;
            foreach (var symbol in segment)
            {
                if (symbol < '0' || symbol > '9')
                    break;
                value = value * 10 + (symbol - '0');
            }

            return value;
        }
    }
}
