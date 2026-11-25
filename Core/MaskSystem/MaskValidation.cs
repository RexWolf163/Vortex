namespace Vortex.Core.MaskSystem
{
    /// <summary>
    /// Результат проверки шаблона маски: валиден ли он, а если нет — что не так и где.
    /// </summary>
    public readonly struct MaskValidation
    {
        /// <summary>Шаблон валиден, маской можно пользоваться.</summary>
        public bool IsValid { get; }

        /// <summary>Описание ошибки. У валидного результата — <c>null</c>.</summary>
        public string Message { get; }

        /// <summary>Позиция ошибки в шаблоне (индекс символа). <c>-1</c> — позиция неизвестна или неприменима.</summary>
        public int Position { get; }

        private MaskValidation(bool isValid, string message, int position)
        {
            IsValid = isValid;
            Message = message;
            Position = position;
        }

        /// <summary>Шаблон валиден.</summary>
        public static MaskValidation Valid => new(true, null, -1);

        /// <summary>Шаблон невалиден: <paramref name="message"/> — описание, <paramref name="position"/> — индекс в шаблоне или <c>-1</c>.</summary>
        public static MaskValidation Invalid(string message, int position = -1) => new(false, message, position);
    }
}
