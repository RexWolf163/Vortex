namespace Vortex.Core.MaskSystem
{
    /// <summary>
    /// Итог проверки настройки маски: верна ли она, а если нет — сообщение и позиция первой ошибки в шаблоне.
    /// </summary>
    public sealed class ValidationResult
    {
        /// <summary>Настройка верна.</summary>
        public bool IsValid { get; }

        /// <summary>Описание ошибки. У верной настройки — <c>null</c>.</summary>
        public string Message { get; }

        /// <summary>
        /// Позиция первой ошибки в шаблоне (индекс символа).
        /// <c>-1</c> — позиция неприменима (вид маски без строки-шаблона) или неизвестна.
        /// </summary>
        public int Position { get; }

        private ValidationResult(bool isValid, string message, int position)
        {
            IsValid = isValid;
            Message = message;
            Position = position;
        }

        /// <summary>Настройка верна.</summary>
        internal static ValidationResult Valid() => new(true, null, -1);

        /// <summary>
        /// Настройка неверна: <paramref name="message"/> — описание,
        /// <paramref name="position"/> — индекс в шаблоне или <c>-1</c>.
        /// </summary>
        internal static ValidationResult Invalid(string message, int position = -1) => new(false, message, position);
    }
}
