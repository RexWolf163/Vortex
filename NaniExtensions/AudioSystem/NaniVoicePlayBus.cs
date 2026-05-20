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
    /// <see cref="TimeController.AddCallback"/> (играющий voice = непустой путь).
    ///
    /// Алгоритм:
    /// 1) <see cref="ITextPrinterManager.OnPrintStarted"/> — фиксируется начало реплики.
    ///    Если предыдущая реплика ещё не закрыта (voice так и не появился, либо ещё играет) — она сливается:
    ///    <see cref="OnVoiceStop"/> для предыдущего автора эмитится здесь же.
    ///    Эмитится <see cref="OnVoiceStart"/>(authorId) по текущему автору, запускается поллинг.
    /// 2) Поллинг видит переход GetPlayedVoice null→непустой — кеширует говоруна. Завершение пойдёт через аудио.
    /// 3) Поллинг видит переход непустой→null с закешированным говоруном — эмитит <see cref="OnVoiceStop"/> и стопает поллинг.
    /// 4) <see cref="ITextPrinterManager.OnPrintFinished"/> — печать закончилась.
    ///    Поллинг и реплика остаются открытыми: voice может прийти async, после печати.
    ///    Закрытие произойдёт по сценарию (3) либо сольётся следующим PrintStart.
    /// </summary>
    public static class NaniVoicePlayBus
    {
        /// <summary>Начало реплики персонажа. Аргумент — authorId говорящего из PrintText.</summary>
        public static event Action<string> OnVoiceStart;

        /// <summary>
        /// Завершение реплики. Если у реплики была голосовая дорожка — authorId берётся из кеша
        /// (зафиксирован в момент старта voice-трека). Иначе — из открытой реплики (current author).
        /// </summary>
        public static event Action<string> OnVoiceStop;

        /// <summary>Автор последней начатой реплики (по PrintStarted). Сбрасывается на закрытии.</summary>
        private static string _currentAuthor;

        /// <summary>Закешированный говорун — выставляется, когда поллинг видит старт voice-трека.</summary>
        private static string _cachedAuthor;

        /// <summary>Предыдущее значение GetPlayedVoice — для детекта transitions в поллинге.</summary>
        private static bool _voiceWasPlaying;

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

            FlushOpenReply();
            StopPolling();
        }

        private static void HandlePrintStart(PrintMessageArgs args)
        {
            // Если предыдущая реплика ещё не закрыта (voice не пришёл, либо ещё играет) — закрываем её здесь.
            FlushOpenReply();

            _currentAuthor = args.Message.Author.Value.Id;
            _cachedAuthor = null;
            OnVoiceStart?.Invoke(_currentAuthor);
            StartPolling();
        }

        private static void HandlePrintFinish(PrintMessageArgs args)
        {
            // НЕ закрываем реплику здесь — voice может прийти async, после конца печати.
            // Закрытие произойдёт либо через поллинг (voice реально стартует и потом остановится),
            // либо при следующем PrintStart (если voice так и не появится).
        }

        private static void HandleNaniStop()
        {
            // Nani остановился (game state change, ScriptPlayer.Stop) — закрываем открытую реплику.
            FlushOpenReply();
            StopPolling();
        }

        /// <summary>
        /// Закрывает текущую открытую реплику, если она ещё не закрыта.
        /// Эмитит OnVoiceStop по cached-автору (если voice играл) либо по current-автору.
        /// </summary>
        private static void FlushOpenReply()
        {
            if (_cachedAuthor != null)
            {
                var author = _cachedAuthor;
                _cachedAuthor = null;
                _currentAuthor = null;
                OnVoiceStop?.Invoke(author);
            }
            else if (_currentAuthor != null)
            {
                var author = _currentAuthor;
                _currentAuthor = null;
                OnVoiceStop?.Invoke(author);
            }
        }

        private static void StartPolling()
        {
            // К моменту PrintStarted voice часто уже играет: команда @print сначала await'ит PlayVoice,
            // потом фаерит OnPrintStarted. Если зафиксировать _voiceWasPlaying=true без кеширования автора,
            // поллинг никогда не увидит transition false→true и реплика не закроется.
            var nowPlaying = !string.IsNullOrEmpty(NaniWrapper.AudioManager.GetPlayedVoice());
            _voiceWasPlaying = nowPlaying;
            if (nowPlaying)
                _cachedAuthor = _currentAuthor;

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
                // voice стартовал — у реплики есть озвучка, переключаем закрытие на аудио-канал.
                _cachedAuthor = _currentAuthor;
            }
            else if (_cachedAuthor != null)
            {
                // voice закончился — закрываем реплику.
                var author = _cachedAuthor;
                _cachedAuthor = null;
                _currentAuthor = null;
                OnVoiceStop?.Invoke(author);
                StopPolling();
            }
        }
    }
}
