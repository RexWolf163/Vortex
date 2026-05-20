using System;
using Naninovel;
using UnityEngine;
using Vortex.Core.AppSystem.Bus;
using Vortex.NaniExtensions.Core;

namespace Vortex.NaniExtensions.AudioSystem
{
    /// <summary>
    /// Шина событий начала/завершения реплики персонажа в Naninovel.
    /// Объединяет два источника — <see cref="ITextPrinterManager"/> и <see cref="IAudioManager"/> —
    /// в единый контракт <see cref="OnVoiceStart"/> / <see cref="OnVoiceStop"/>, где аргументом идёт ключ говорящего.
    ///
    /// Алгоритм:
    /// 1) <see cref="ITextPrinterManager.OnPrintTextStarted"/> — фиксируется начало реплики.
    ///    Эмитится <see cref="OnVoiceStart"/>(authorId) сразу, по текущему автору печати.
    /// 2) Если за этим следует <see cref="IAudioManager.OnVoicePlayStarted"/> — кешируем говоруна
    ///    (= у реплики есть голосовая дорожка). Завершение пойдёт через аудиоканал.
    /// 3) Завершение:
    ///    - если есть закешированный говорун → ждём <see cref="IAudioManager.OnVoicePlayStopped"/>,
    ///      эмитим <see cref="OnVoiceStop"/>(cachedAuthor);
    ///    - иначе → ждём <see cref="ITextPrinterManager.OnPrintTextFinished"/>,
    ///      эмитим <see cref="OnVoiceStop"/>(args.AuthorId).
    /// </summary>
    public static class NaniVoicePlayBus
    {
        /// <summary>Начало реплики персонажа. Аргумент — authorId говорящего из PrintText.</summary>
        public static event Action<string> OnVoiceStart;

        /// <summary>
        /// Завершение реплики. Если у реплики была голосовая дорожка — authorId берётся из кеша
        /// (зафиксирован в момент старта voice-трека). Иначе — из аргументов PrintTextFinished.
        /// </summary>
        public static event Action<string> OnVoiceStop;

        /// <summary>Автор последней начатой реплики (по PrintTextStarted). Сбрасывается на следующем старте.</summary>
        private static string _currentAuthor;

        /// <summary>Закешированный говорун — выставляется, если за PrintStart пришёл VoicePlayStarted.</summary>
        private static string _cachedAuthor;

        private static bool _wasStarted;

        [RuntimeInitializeOnLoadMethod]
        private static void Run()
        {
            App.OnStart -= Init;
            App.OnStart += Init;
            App.OnExit -= Dispose;
            App.OnExit += Dispose;
        }

        private static void Init()
        {
            if (_wasStarted) return;
            _wasStarted = true;

            NaniWrapper.TextPrinterManager.OnPrintTextStarted += HandlePrintStart;
            NaniWrapper.TextPrinterManager.OnPrintTextFinished += HandlePrintFinish;
            NaniWrapper.AudioManager.OnVoicePlayStarted += HandleVoiceStart;
            NaniWrapper.AudioManager.OnVoicePlayStopped += HandleVoiceStop;
        }

        private static void Dispose()
        {
            if (!_wasStarted) return;
            _wasStarted = false;

            NaniWrapper.TextPrinterManager.OnPrintTextStarted -= HandlePrintStart;
            NaniWrapper.TextPrinterManager.OnPrintTextFinished -= HandlePrintFinish;
            NaniWrapper.AudioManager.OnVoicePlayStarted -= HandleVoiceStart;
            NaniWrapper.AudioManager.OnVoicePlayStopped -= HandleVoiceStop;

            _currentAuthor = null;
            _cachedAuthor = null;
        }

        private static void HandlePrintStart(PrintTextArgs args)
        {
            _currentAuthor = args.AuthorId;
            // На каждой новой реплике сбрасываем кеш — если у предыдущей был voice,
            // её OnVoiceStop уже эмитился по VoicePlayStopped (Nani останавливает прошлый voice перед стартом нового).
            _cachedAuthor = null;
            OnVoiceStart?.Invoke(_currentAuthor);
        }

        private static void HandleVoiceStart(string clipPath)
        {
            // У текущей реплики есть голосовая дорожка → переключаем закрытие на аудио-канал.
            _cachedAuthor = _currentAuthor;
        }

        private static void HandleVoiceStop(string clipPath)
        {
            // Voice-трек завершился без активной реплики — посторонний voice (ручной [playVoice] и т. п.). Игнор.
            if (_cachedAuthor == null) return;
            var author = _cachedAuthor;
            _cachedAuthor = null;
            OnVoiceStop?.Invoke(author);
        }

        private static void HandlePrintFinish(PrintTextArgs args)
        {
            // Если есть кешированный говорун — закрытие реплики возьмёт на себя HandleVoiceStop.
            if (_cachedAuthor != null) return;
            OnVoiceStop?.Invoke(args.AuthorId);
            _currentAuthor = null;
        }
    }
}
