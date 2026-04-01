namespace Vortex.Core.Extensions.LogicExtensions
{
    public static class StringExtCommons
    {
        public static bool IsNullOrWhitespace(this string str) => string.IsNullOrEmpty(str?.Trim());
    }
}