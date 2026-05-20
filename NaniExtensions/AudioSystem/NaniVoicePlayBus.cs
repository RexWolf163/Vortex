using System;
using Naninovel;
using UnityEngine;
using Vortex.Core.AppSystem.Bus;
using Vortex.NaniExtensions.Core;
using Vortex.Unity.AppSystem.System.TimeSystem;

namespace Vortex.NaniExtensions.AudioSystem
{
    /// <summary>
    /// Шина событий начала/завершения реплики персонажа в Naninovel.
    /// Объединяет два источника — <see cref="ITextPrinterManager"/> и <see cref="IAudioManager"/> —
    /// в единый контракт <see cref="OnVoiceStart"/> / <see cref="OnVoiceStop"/>, где аргументом идёт ключ говорящего.
    ///
    /// Воспроизведение голоса детектируется опросом <see cref="IAudioManager.GetPlayedVoice"/>:
    /// API возвращает путь играющего voice-клипа или null. Отслеживая путь, мы корректно ловим:
    ///   - конец voice (path → null);
    ///   - смену voice на другого актора (pathA → pathB) — даже если null-окна между ними нет.
    ///
    /// Алгоритм:
    /// 1) <see cref="ITextPrinterManager.OnPrintStarted"/>:
    ///    - если у текущей реплики не было собственной озвучки (cachedAuthor != currentAuthor),
    ///      закрываем её сразу <see cref="OnVoiceStop"/>(currentAuthor);
    ///    - если cachedAuthor == currentAuthor, оставляем — её voice ещё играет и закроется по аудио;
    ///    - эмитим <see cref="OnVoiceStart"/>(newAuthor), стартует/продолжается поллинг.
    /// 2) Поллинг ловит любое изменение path:
    ///    - oldPath != null → эмитим <see cref="OnVoiceStop"/>(cachedAuthor);
    ///    - newPath != null → cachedAuthor = currentAuthor (новая озвучка принадлежит активной реплике).
    /// 3) <see cref="ITextPrinterManager.OnPrintFinished"/> — ничего не делает: voice может прийти async,
    ///    закрытие всегда идёт либо через поллинг, либо через следующий PrintStarted.
    /// </summary>
    public static class NaniVoicePlayBus
    {
        /// <summary>Начало реплики персонажа. Аргумент — authorId говорящего из PrintText.</summary>
        public static event Action<string> OnVoiceStart;

        /// <summary>
        /// Завершение реплики. Если у реплики была голосовая дорожка — закрывается по окончании voice
        /// (authorId из кеша). Если voice не было — закрывается на следующей PrintStarted либо при Dispose.
        /// </summary>
        public static event Action<string> OnVoiceStop;

        /// <summary>Автор последней начатой реплики (по PrintStarted).</summary>
        private static string _currentAuthor;

        /// <summary>Автор, чья voice-дорожка сейчас играет (либо играла, и сейчас закрывается).</summary>
        private static string _cachedAuthor;

        /// <summary>Путь играющего voice-клипа (или null). Используется для детекта transitions.</summary>
        private static string _cachedVoicePath;

        /// <summary>Активен ли поллинг voice.</summary>
        private static bool _polling;

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

            NaniWrapper.TextPrinterManager.OnPrintStarted += HandlePrintStart;
            NaniWrapper.TextPrinterManager.OnPrintFinished += HandlePrintFinish;
            NaniWrapper.OnNaniStop += HandleNaniStop;
        }

        private static void Dispose()
        {
            if (!_wasStarted) return;
            _wasStarted = false;

            NaniWrapper.TextPrinterManager.OnPrintStarted -= HandlePrintStart;
            NaniWrapper.TextPrinterManager.OnPrintFinished -= HandlePrintFinish;
            NaniWrapper.OnNaniStop -= HandleNaniStop;

            FlushAll();
            StopPolling();
        }

        private static void HandlePrintStart(PrintMessageArgs args)
        {
            // Если текущая реплика не имела собственной voice-дорожки (cached != current) —
            // закрываем её сейчас. Если имела (cached == current) — оставляем, её voice ещё играет
            // и закроется через поллинг при окончании аудио.
            if (_currentAuthor != null && _cachedAuthor != _currentAuthor)
            {
                var prev = _currentAuthor;
                _currentAuthor = null;
                OnVoiceStop?.Invoke(prev);
            }

            _currentAuthor = args.Message.Author.Value.Id;
            OnVoiceStart?.Invoke(_currentAuthor);

            if (!_polling)
            {
                _polling = true;
                TimeController.AddCallback(PollVoice);
            }

            // Реконсилируем состояние сразу: voice мог уже играть к моменту OnPrintStarted
            // (Naninovel @print awaitит PlayVoice до фаер'инга OnPrintStarted).
            PollVoice();
        }

        private static void HandlePrintFinish(PrintMessageArgs args)
        {
            // Никаких действий — закрытие реплики идёт либо через поллинг (voice реально остановился
            // или сменился), либо через следующий PrintStarted (если voice вообще не было).
        }

        private static void HandleNaniStop()
        {
            FlushAll();
            StopPolling();
        }

        /// <summary>
        /// Закрывает все открытые реплики (cached и current, если они разные).
        /// </summary>
        private static void FlushAll()
        {
            var cached = _cachedAuthor;
            var current = _currentAuthor;

            _cachedAuthor = null;
            _cachedVoicePath = null;
            _currentAuthor = null;

            if (cached != null)
                OnVoiceStop?.Invoke(cached);
            if (current != null && current != cached)
                OnVoiceStop?.Invoke(current);
        }

        private static void StopPolling()
        {
            if (!_polling) return;
            _polling = false;
            TimeController.RemoveCallback(PollVoice);
        }

        private static void PollVoice()
        {
            var newPath = NaniWrapper.AudioManager.GetPlayedVoice();
            if (string.IsNullOrEmpty(newPath)) newPath = null;
            if (newPath == _cachedVoicePath) return;

            var oldPath = _cachedVoicePath;
            _cachedVoicePath = newPath;

            // Старый voice закончился (естественно) или сменился на другой — закрываем его реплику.
            if (oldPath != null && _cachedAuthor != null)
            {
                var author = _cachedAuthor;
                _cachedAuthor = null;
                OnVoiceStop?.Invoke(author);
            }

            // Новый voice появился — назначаем его текущей активной реплике.
            if (newPath != null)
                _cachedAuthor = _currentAuthor;
        }
    }
}
