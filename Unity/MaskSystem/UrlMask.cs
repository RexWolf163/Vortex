using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using Vortex.Core.MaskSystem;

namespace Vortex.Unity.MaskSystem
{
    /// <summary>
    /// Маска вида «URL»: настройка проверяемых частей адреса, подготовка входа (разбор в адрес)
    /// и подключение к <see cref="UrlController"/>.
    /// </summary>
    [Serializable]
    public class UrlMask : UnityMask
    {
        [SerializeField, LabelText("Схема"), PropertyTooltip("$RulesHint"), OnValueChanged("OnSettingsChanged")]
        private SchemeCheckMode schemeMode = SchemeCheckMode.Any;

        [SerializeField, LabelText("Набор схем"), PropertyTooltip("$RulesHint"),
         OnValueChanged("OnSettingsChanged", true)]
        private string[] schemes = new string[0];

        [SerializeField, LabelText("Хост"), PropertyTooltip("$RulesHint"), OnValueChanged("OnSettingsChanged")]
        private HostCheckMode hostMode = HostCheckMode.Any;

        [SerializeField, LabelText("Значение хоста"), PropertyTooltip("$RulesHint"),
         OnValueChanged("OnSettingsChanged")]
        private string host = "";

        [SerializeField, LabelText("Порт"), PropertyTooltip("$RulesHint"), OnValueChanged("OnSettingsChanged")]
        private PortCheckMode portMode = PortCheckMode.Any;

        [SerializeField, LabelText("Значение порта"), PropertyTooltip("$RulesHint"),
         OnValueChanged("OnSettingsChanged")]
        private int port;

        [SerializeField, LabelText("Путь"), PropertyTooltip("$RulesHint"), OnValueChanged("OnSettingsChanged")]
        private PathCheckMode pathMode = PathCheckMode.Any;

        [SerializeField, LabelText("Значение пути"), PropertyTooltip("$RulesHint"),
         OnValueChanged("OnSettingsChanged")]
        private string path = "";

        [SerializeField, LabelText("Учитывать регистр пути"), PropertyTooltip("$RulesHint"),
         OnValueChanged("OnSettingsChanged")]
        private bool pathCaseSensitive = true;

        [NonSerialized] private ValidationResult _validation;
        [NonSerialized] private string _idnHost;

        /// <summary>Режим проверки схемы.</summary>
        public SchemeCheckMode SchemeMode => schemeMode;

        /// <summary>Набор схем.</summary>
        public IReadOnlyList<string> Schemes => schemes;

        /// <summary>Режим проверки хоста.</summary>
        public HostCheckMode HostMode => hostMode;

        /// <summary>Значение хоста.</summary>
        public string Host => host;

        /// <summary>Режим проверки порта.</summary>
        public PortCheckMode PortMode => portMode;

        /// <summary>Значение порта.</summary>
        public int Port => port;

        /// <summary>Режим проверки пути.</summary>
        public PathCheckMode PathMode => pathMode;

        /// <summary>Значение пути.</summary>
        public string Path => path;

        /// <summary>Учитывать регистр пути.</summary>
        public bool PathCaseSensitive => pathCaseSensitive;

        /// <inheritdoc/>
        public override string RulesHint =>
            "Вход разбирается как абсолютный URL вида схема://…; не разобрался — не совпало.\n" +
            "Не URL (не совпало): вход с «\\», управляющими символами (в том числе перевод строки) " +
            "или пробелами по краям.\n" +
            "Значения хоста и пути — в той форме, в которой их отдаёт разбор адреса; иначе ошибка настройки " +
            "подскажет нужную форму.\n" +
            "Проверяются только выбранные части, остальные — любые; совпадение, если подошли все проверенные.\n" +
            "Схема и хост — без учёта регистра; хосты сравниваются в ASCII-форме (IDN — punycode), " +
            "хост входа с недопустимыми символами не совпадает.\n" +
            "Хост и поддомены — по границе метки: partner.com подходит для cdn.partner.com, но не для evilpartner.com.\n" +
            "Порт по умолчанию схемы равен явному: https://h.com — порт 443.\n" +
            "Путь — в экранированном виде; префикс — по границе сегмента: /v2 подходит для /v2 и /v2/x, но не для /v20.\n" +
            "Query и фрагмент не проверяются.";

        /// <inheritdoc/>
        public override ValidationResult Validate() =>
            _validation ??= UrlController.FirstInvalid(
                UrlController.ValidateScheme(schemeMode, schemes),
                UrlController.ValidateHost(hostMode, host),
                UrlController.ValidatePort(portMode, port),
                UrlController.ValidatePath(pathMode, path));

        /// <inheritdoc/>
        public override bool IsMatch(string input)
        {
            if (input == null)
                throw Fail(new ArgumentNullException(nameof(input), $"[{GetType().Name}] Вход не задан (null)."));
            RequireValid();
            var ready = Prepare(input);
            return ready != null && UrlController.IsAll(
                UrlController.IsSchemeMatch(ready, schemeMode, schemes),
                UrlController.IsHostMatch(ready, hostMode,
                    hostMode == HostCheckMode.Any ? null : _idnHost ??= UrlController.ToIdnHost(host)),
                UrlController.IsPortMatch(ready, portMode, port),
                UrlController.IsPathMatch(ready, pathMode, path, !pathCaseSensitive));
        }

        /// <inheritdoc/>
        public override MatchResult Match(string input) =>
            IsMatch(input) ? MatchResult.Matched() : MatchResult.NoMatch();

        /// <summary>
        /// Разбор входа в абсолютный адрес вида <c>схема://…</c>, сохраняющий написанную схему;
        /// не разобрался — <c>null</c>. Адресом не считается:
        /// вход с «\», управляющими символами или пробелами по краям
        /// (<see cref="Uri"/> мягче HTTP-стека и мог бы понять такую строку иначе, чем потребитель);
        /// вход без <c>схема://</c> — пути (<c>/a/b</c>, <c>c:/x</c>) и адреса без полномочия (<c>mailto:</c>),
        /// которые <see cref="Uri"/> на разных платформах разбирает по-разному.
        /// </summary>
        private static Uri Prepare(string input)
        {
            if (input.Length == 0 || char.IsWhiteSpace(input[0]) || char.IsWhiteSpace(input[^1]))
                return null;
            foreach (var c in input)
                if (c == '\\' || char.IsControl(c))
                    return null;
            var separator = input.IndexOf("://", StringComparison.Ordinal);
            if (separator <= 0)
                return null;
            var scheme = input.Substring(0, separator);
            if (!Uri.CheckSchemeName(scheme) || !Uri.TryCreate(input, UriKind.Absolute, out var uri))
                return null;
            // Разбор должен сохранить написанную схему: иначе Uri прочитал вход иначе, чем он написан
            // (например, «c://x» — как путь DOS со схемой file)
            return string.Equals(uri.Scheme, scheme, StringComparison.OrdinalIgnoreCase) ? uri : null;
        }

        private void RequireValid()
        {
            var validation = Validate();
            if (!validation.IsValid)
                throw Fail(new InvalidOperationException(
                    $"[{GetType().Name}] Сопоставление на неверной настройке: {validation.Message}"));
        }

        private static Exception Fail(Exception exception)
        {
            Debug.LogError(exception.Message);
            return exception;
        }

#if UNITY_EDITOR
        private void OnSettingsChanged()
        {
            _validation = null;
            _idnHost = null;
            ResetEditorCache();
        }
#endif
    }
}
