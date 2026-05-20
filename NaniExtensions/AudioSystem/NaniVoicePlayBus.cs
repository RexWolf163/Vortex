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
    /// Поскольку <see cref="IAudioManager"/> в текущей версии Naninovel не эмитит событий начала/конца voice,
    /// факт наличия voice-дорожки определяется опросом <see cref="IAudioManager.GetPlayedVoice"/> по тику
    /// <see cref="TimeController.AddCallback"/> (играющий voice = непустой путь). Поллинг работает только
    /// в окне активной реплики (между PrintStarted и её закрытием), вне реплики ничего не тратит.
    ///
    /// Алгоритм:
    /// 1) <see cref="ITextPrinterManager.OnPrintStarted"/> — фиксируется начало реплики.
    ///    Эмитится <see cref="OnVoiceStart"/>(authorId) сразу по текущему автору печати, запускается поллинг voice.
    /// 2) Если поллинг видит переход GetPlayedVoice null→непустой — кешируем говоруна
    ///    (= у реплики есть голосовая дорожка). Завершение пойдёт по аудиоканалу.
    /// 3) Завершение:
    ///    - если есть закешированный говорун → ждём GetPlayedVoice непустой→null,
    ///      эмитим <see cref="OnVoiceStop"/>(cachedAuthor);
    ///    - иначе → ждём <see cref="ITextPrinterManager.OnPrintFinished"/>,
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

        /// <summary>Автор последней начатой реплики (по PrintTextStarted). Сбрасывается на финише.</summary>
        private static string _currentAuthor;

        /// <summary>Закешированный говорун — выставляется, когда поллинг видит старт voice-трека.</summary>
        private static string _cachedAuthor;

        /// <summary>Предыдущее значение IsVoicePlaying — для детекта transitions.</summary>
        private static bool _voiceWasPlaying;

        /// <summary>Активен ли поллинг voice (есть открытая реплика).</summary>
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
        }

        private static void Dispose()
        {
            if (!_wasStarted) return;
            _wasStarted = false;

            NaniWrapper.TextPrinterManager.OnPrintStarted -= HandlePrintStart;
            NaniWrapper.TextPrinterManager.OnPrintFinished -= HandlePrintFinish;
            StopPolling();

            _currentAuthor = null;
            _cachedAuthor = null;
        }

        private static void HandlePrintStart(PrintMessageArgs args)
        {
            _currentAuthor = args.Message.Author.Value.Id;
            // На каждой новой реплике сбрасываем кеш — если у предыдущей был voice,
            // её OnVoiceStop уже эмитился по переходу voice→stop в поллинге.
            _cachedAuthor = null;
            OnVoiceStart?.Invoke(_currentAuthor);
            StartPolling();
        }

        private static void HandlePrintFinish(PrintMessageArgs args)
        {
            // Если есть кешированный говорун — закрытие реплики возьмёт на себя поллинг при остановке voice.
            if (_cachedAuthor != null) return;

            OnVoiceStop?.Invoke(args.Message.Author.Value.Id);
            _currentAuthor = null;
            StopPolling();
        }

        private static void StartPolling()
        {
            _voiceWasPlaying = !string.IsNullOrEmpty(NaniWrapper.AudioManager.GetPlayedVoice());
            if (_polling) return;
            _polling = true;
            TimeController.AddCallback(PollVoice);
        }

        private static void StopPolling()
        {
            if (!_polling) return;
            _polling = false;
            TimeController.RemoveCallback(PollVoice);
            _voiceWasPlaying = false;
        }

        private static void PollVoice()
        {
            var nowPlaying = !string.IsNullOrEmpty(NaniWrapper.AudioManager.GetPlayedVoice());
            if (nowPlaying == _voiceWasPlaying) return;
            _voiceWasPlaying = nowPlaying;

            if (nowPlaying)
            {
                // voice-трек стартовал — у реплики есть озвучка, переключаем закрытие на аудиоканал.
                _cachedAuthor = _currentAuthor;
            }
            else if (_cachedAuthor != null)
            {
                // voice-трек закончился — закрываем реплику и останавливаем поллинг.
                var author = _cachedAuthor;
                _cachedAuthor = null;
                _currentAuthor = null;
                OnVoiceStop?.Invoke(author);
                StopPolling();
            }
        }
    }
}
