using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Vortex.Core.LoggerSystem.Bus;
using Vortex.Core.LoggerSystem.Model;

namespace Vortex.Core.MaskSystem
{
    /// <summary>
    /// Правило «адрес подходит»: проверка настройки каждой части адреса, разбор входа в адрес, сравнение каждой
    /// части разобранного адреса с настройкой и итог «подошли все проверенные части».
    /// </summary>
    /// <remarks>
    /// Примеры режимов:
    /// <list type="bullet">
    /// <item>Хост и поддомены <c>partner.com</c>: совпадает с <c>partner.com</c> и <c>cdn.partner.com</c>,
    /// но не с <c>evilpartner.com</c>.</item>
    /// <item>Префикс пути <c>/v2</c>: совпадает с <c>/v2</c> и <c>/v2/x</c>, но не с <c>/v20</c>.</item>
    /// <item>Порт точно <c>443</c>: совпадает с <c>https://h.com/</c> (порт по умолчанию схемы).</item>
    /// </list>
    /// </remarks>
    public static class UrlController
    {
        #region Проверка настройки

        /// <summary>Проверить настройку схемы.</summary>
        public static ValidationResult ValidateScheme(SchemeCheckMode mode, IReadOnlyList<string> schemes)
        {
            if (!Enum.IsDefined(typeof(SchemeCheckMode), mode))
                return ValidationResult.Invalid($"Неизвестный режим схемы: {(int)mode}.");
            if (mode == SchemeCheckMode.Any)
                return ValidationResult.Valid();
            if (schemes == null || schemes.Count == 0)
                return ValidationResult.Invalid("Набор схем пуст.");
            foreach (var scheme in schemes)
            {
                // Каноническая форма: схема должна разобраться в адресе и остаться той же
                if (scheme == null || !Uri.CheckSchemeName(scheme)
                                   || !Uri.TryCreate(scheme + "://h.test/", UriKind.Absolute, out var uri)
                                   || !string.Equals(uri.Scheme, scheme, StringComparison.OrdinalIgnoreCase))
                    return ValidationResult.Invalid(
                        $"Схема «{scheme}» не распознаётся как схема адреса: только имя схемы, без «:» и пробелов " +
                        "(например, https).");
            }

            return ValidationResult.Valid();
        }

        /// <summary>
        /// Проверить настройку хоста: ASCII-форма хоста — допустимое имя, а сама настройка совпадает
        /// с этой ASCII-формой (punycode) или с её обратным Unicode-преобразованием; IP-адрес — со своей
        /// канонической формой в адресе.
        /// </summary>
        public static ValidationResult ValidateHost(HostCheckMode mode, string host)
        {
            if (!Enum.IsDefined(typeof(HostCheckMode), mode))
                return ValidationResult.Invalid($"Неизвестный режим хоста: {(int)mode}.");
            if (mode == HostCheckMode.Any)
                return ValidationResult.Valid();
            if (string.IsNullOrEmpty(host))
                return ValidationResult.Invalid("Хост не задан.");

            if (!Uri.TryCreate("http://" + host + "/", UriKind.Absolute, out var uri)
                || !string.Equals(uri.Authority, uri.Host, StringComparison.OrdinalIgnoreCase))
            {
                var hint = Uri.CheckHostName(host) == UriHostNameType.IPv6 && host[0] != '['
                    ? $" IPv6-адрес указывается в квадратных скобках: [{host}]."
                    : string.Empty;
                return ValidationResult.Invalid($"Хост «{host}» нельзя разобрать как хост адреса.{hint}");
            }

            if (uri.HostNameType != UriHostNameType.Dns && uri.HostNameType != UriHostNameType.IPv4
                                                        && uri.HostNameType != UriHostNameType.IPv6)
                return ValidationResult.Invalid($"«{host}» не является допустимым именем хоста.");
            if (!TryGetIdnHost(uri, out var idnHost))
                return ValidationResult.Invalid($"Хост «{host}» содержит символы, недопустимые в имени хоста.");

            if (uri.HostNameType == UriHostNameType.Dns)
            {
                string unicodeHost;
                try
                {
                    unicodeHost = new IdnMapping().GetUnicode(idnHost);
                }
                catch (ArgumentException)
                {
                    return ValidationResult.Invalid($"Хост «{host}» содержит символы, недопустимые в имени хоста.");
                }

                if (!string.Equals(host, idnHost, StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(host, unicodeHost, StringComparison.OrdinalIgnoreCase))
                    return string.Equals(NormalizeOrNull(host), unicodeHost, StringComparison.OrdinalIgnoreCase)
                        ? ValidationResult.Invalid(
                            $"Хост «{host}» отличается от «{unicodeHost}» формой Unicode (NFC): " +
                            "используйте нормализованную запись.")
                        : ValidationResult.Invalid(
                            $"Хост «{host}» в адресе выглядит как «{unicodeHost}» ({idnHost}): используйте эту форму.");
            }
            else if (!string.Equals(uri.Host, host, StringComparison.OrdinalIgnoreCase))
                return ValidationResult.Invalid(
                    $"Хост «{host}» в адресе выглядит как «{uri.Host}»: используйте эту форму.");

            if (mode == HostCheckMode.WithSubdomains && uri.HostNameType != UriHostNameType.Dns)
                return ValidationResult.Invalid($"У IP-адреса «{host}» нет поддоменов: используйте режим «точно».");
            return ValidationResult.Valid();
        }

        /// <summary>
        /// ASCII-форма проверенного хоста (IDN — в punycode, IPv6 — без скобок): в ней хосты сравниваются.
        /// </summary>
        public static string ToIdnHost(string host)
        {
            if (string.IsNullOrEmpty(host)
                || !Uri.TryCreate("http://" + host + "/", UriKind.Absolute, out var uri)
                || !TryGetIdnHost(uri, out var idnHost))
                throw Fail(new ArgumentException($"Хост «{host}» не проверен или неверен.", nameof(host)));
            return idnHost;
        }

        private static string NormalizeOrNull(string value)
        {
            try
            {
                return value.Normalize(NormalizationForm.FormC);
            }
            catch (ArgumentException)
            {
                return null;
            }
        }

        /// <summary>
        /// ASCII-форма хоста адреса, если она — допустимое имя хоста (DNS, IPv4, IPv6). IDNA-отображение может
        /// без исключения дать строку с пробелом, «;», «,», «|», «%» — такая форма недопустима.
        /// </summary>
        private static bool TryGetIdnHost(Uri uri, out string idnHost)
        {
            try
            {
                idnHost = uri.IdnHost;
            }
            catch (UriFormatException)
            {
                idnHost = null;
                return false;
            }

            var type = Uri.CheckHostName(idnHost);
            if (type == UriHostNameType.Dns || type == UriHostNameType.IPv4 || type == UriHostNameType.IPv6)
                return true;
            idnHost = null;
            return false;
        }

        /// <summary>Проверить настройку порта.</summary>
        public static ValidationResult ValidatePort(PortCheckMode mode, int port)
        {
            if (!Enum.IsDefined(typeof(PortCheckMode), mode))
                return ValidationResult.Invalid($"Неизвестный режим порта: {(int)mode}.");
            if (mode == PortCheckMode.Any)
                return ValidationResult.Valid();
            if (port < 1 || port > 65535)
                return ValidationResult.Invalid($"Порт {port} вне диапазона 1–65535.");
            return ValidationResult.Valid();
        }

        /// <summary>
        /// Проверить настройку пути: начинается с «/» и совпадает со своей канонической формой в адресе
        /// (экранирование, точки-сегменты — как их отдаёт разбор входа).
        /// </summary>
        public static ValidationResult ValidatePath(PathCheckMode mode, string path)
        {
            if (!Enum.IsDefined(typeof(PathCheckMode), mode))
                return ValidationResult.Invalid($"Неизвестный режим пути: {(int)mode}.");
            if (mode == PathCheckMode.Any)
                return ValidationResult.Valid();
            if (string.IsNullOrEmpty(path))
                return ValidationResult.Invalid("Путь не задан.");
            if (path[0] != '/')
                return ValidationResult.Invalid("Путь должен начинаться с «/».", 0);

            if (!Uri.TryCreate("http://h.test" + path, UriKind.Absolute, out var uri))
                return ValidationResult.Invalid($"Путь «{path}» нельзя разобрать как путь адреса.");
            if (!string.Equals(uri.AbsolutePath, path, StringComparison.Ordinal))
                return ValidationResult.Invalid(
                    $"Путь «{path}» в адресе выглядит как «{uri.AbsolutePath}»: используйте эту форму.");
            return ValidationResult.Valid();
        }

        /// <summary>Первый неверный итог из перечисленных; все верны — верный итог.</summary>
        public static ValidationResult FirstInvalid(params ValidationResult[] results)
        {
            foreach (var result in results)
                if (!result.IsValid)
                    return result;
            return ValidationResult.Valid();
        }

        #endregion

        #region Разбор входа

        /// <summary>
        /// Разобрать вход в абсолютный адрес вида <c>схема://…</c>, сохраняющий написанную схему. Адресом не считается:
        /// вход с «\», управляющими символами или пробелами по краям
        /// (<see cref="Uri"/> мягче HTTP-стека и мог бы понять такую строку иначе, чем потребитель);
        /// вход без <c>схема://</c> — пути (<c>/a/b</c>, <c>c:/x</c>) и адреса без полномочия (<c>mailto:</c>),
        /// которые <see cref="Uri"/> на разных платформах разбирает по-разному.
        /// </summary>
        /// <returns><c>true</c> — адрес разобран; <c>false</c> — вход не адрес (это данные, а не ошибка: «не совпало»).</returns>
        public static bool TryParseInput(string input, out Uri uri)
        {
            if (input == null)
                throw Fail(new ArgumentNullException(nameof(input), "Вход не задан (null)."));
            uri = null;
            if (input.Length == 0 || char.IsWhiteSpace(input[0]) || char.IsWhiteSpace(input[^1]))
                return false;
            foreach (var c in input)
                if (c == '\\' || char.IsControl(c))
                    return false;
            var separator = input.IndexOf("://", StringComparison.Ordinal);
            if (separator <= 0)
                return false;
            var scheme = input.Substring(0, separator);
            if (!Uri.CheckSchemeName(scheme) || !Uri.TryCreate(input, UriKind.Absolute, out var parsed))
                return false;
            // Разбор должен сохранить написанную схему: иначе Uri прочитал вход иначе, чем он написан
            // (например, «c://x» — как путь DOS со схемой file)
            if (!string.Equals(parsed.Scheme, scheme, StringComparison.OrdinalIgnoreCase))
                return false;
            uri = parsed;
            return true;
        }

        #endregion

        #region Сопоставление частей

        /// <summary>Подходит ли схема адреса.</summary>
        public static bool IsSchemeMatch(Uri uri, SchemeCheckMode mode, IReadOnlyList<string> schemes)
        {
            RequireUri(uri);
            switch (mode)
            {
                case SchemeCheckMode.Any:
                    return true;
                case SchemeCheckMode.FromSet:
                    if (schemes == null)
                        throw Fail(new ArgumentNullException(nameof(schemes), "Набор схем не задан (null)."));
                    foreach (var scheme in schemes)
                        if (string.Equals(uri.Scheme, scheme, StringComparison.OrdinalIgnoreCase))
                            return true;
                    return false;
                default:
                    throw Fail(new ArgumentOutOfRangeException(nameof(mode), mode, "Неизвестный режим схемы."));
            }
        }

        /// <summary>
        /// Подходит ли хост адреса. Сравнение — в ASCII-форме: <paramref name="idnHost"/> получен через
        /// <see cref="ToIdnHost"/>; хост входа, у которого ASCII-формы нет (недопустимые символы), не подходит.
        /// </summary>
        public static bool IsHostMatch(Uri uri, HostCheckMode mode, string idnHost)
        {
            RequireUri(uri);
            if (mode != HostCheckMode.Any && string.IsNullOrEmpty(idnHost))
                throw Fail(new ArgumentException("Хост не задан.", nameof(idnHost)));
            if (mode == HostCheckMode.Any)
                return true;
            // Хост входа — данные: без ASCII-формы он не совпадает ни с чем
            if (!TryGetIdnHost(uri, out var actual))
                return false;
            switch (mode)
            {
                case HostCheckMode.Exact:
                    return string.Equals(actual, idnHost, StringComparison.OrdinalIgnoreCase);
                case HostCheckMode.WithSubdomains:
                    return string.Equals(actual, idnHost, StringComparison.OrdinalIgnoreCase)
                           || actual.EndsWith("." + idnHost, StringComparison.OrdinalIgnoreCase);
                default:
                    throw Fail(new ArgumentOutOfRangeException(nameof(mode), mode, "Неизвестный режим хоста."));
            }
        }

        /// <summary>Подходит ли порт адреса. Порт по умолчанию схемы равен явному.</summary>
        public static bool IsPortMatch(Uri uri, PortCheckMode mode, int port)
        {
            RequireUri(uri);
            return mode switch
            {
                PortCheckMode.Any => true,
                PortCheckMode.Exact => uri.Port == port,
                _ => throw Fail(new ArgumentOutOfRangeException(nameof(mode), mode, "Неизвестный режим порта."))
            };
        }

        /// <summary>Подходит ли путь адреса (в экранированном виде; пустой путь — «/»).</summary>
        public static bool IsPathMatch(Uri uri, PathCheckMode mode, string path, bool ignoreCase)
        {
            RequireUri(uri);
            if (mode != PathCheckMode.Any && string.IsNullOrEmpty(path))
                throw Fail(new ArgumentException("Путь не задан.", nameof(path)));
            var comparison = ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            var actual = uri.AbsolutePath.Length == 0 ? "/" : uri.AbsolutePath;
            switch (mode)
            {
                case PathCheckMode.Any:
                    return true;
                case PathCheckMode.Exact:
                    return string.Equals(actual, path, comparison);
                case PathCheckMode.Prefix:
                    if (string.Equals(actual, path, comparison))
                        return true;
                    if (!actual.StartsWith(path, comparison))
                        return false;
                    // Граница сегмента: префикс оканчивается «/» или следующий символ пути — «/»
                    return path[^1] == '/' || actual[path.Length] == '/';
                default:
                    throw Fail(new ArgumentOutOfRangeException(nameof(mode), mode, "Неизвестный режим пути."));
            }
        }

        /// <summary>Адрес подходит, если подошли все проверенные части.</summary>
        public static bool IsAll(bool scheme, bool host, bool port, bool path) => scheme && host && port && path;

        #endregion

        private static void RequireUri(Uri uri)
        {
            if (uri == null)
                throw Fail(new ArgumentNullException(nameof(uri), "Разобранный адрес не задан (null)."));
        }

        private static Exception Fail(Exception exception)
        {
            Log.Print(LogLevel.Error, exception.Message, nameof(UrlController));
            return exception;
        }
    }
}
