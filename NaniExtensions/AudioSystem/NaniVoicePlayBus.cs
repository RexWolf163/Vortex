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
    /// 3) <see cref="ITextPrinterManager.OnPrintFinished"/>:
    ///    - если у завершившейся реплики voice не было (_cachedAuthor != _currentAuthor),
    ///      эмитим <see cref="OnVoiceStop"/>(_currentAuthor) сразу — иначе при отсутствии
    ///      следующего PrintStarted (конец диалога, ожидание выбора) подписчик залипает;
    ///    - если voice играет, ничего не делаем: voice имеет право продолжаться после печати,
    ///      закрытие придёт через поллинг.
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
            var newAuthor = args.Message.Author.Value.Id;

            // Тихий переход: тот же говорун продолжает с новой voice-дорожкой.
            // Не эмитим ни Stop, ни Start — аниматор остаётся в Forward, новый voice заменит старый.
            var silent = newAuthor == _currentAuthor && _cachedAuthor == newAuthor;

            if (!silent)
            {
                // Закрываем предыдущую реплику, если у неё не было собственной озвучки.
                // Если была (cached == current) — оставляем, её voice ещё играет и закроется через поллинг.
                if (_currentAuthor != null && _cachedAuthor != _currentAuthor)
                {
                    var prev = _currentAuthor;
                    _currentAuthor = null;
                    OnVoiceStop?.Invoke(prev);
                }

                _currentAuthor = newAuthor;
                OnVoiceStart?.Invoke(newAuthor);
            }
            else
            {
                _currentAuthor = newAuthor; // безопасное переприсваивание (тот же id)
            }

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
            // Если у завершившейся реплики не было собственной voice-дорожки
            // (_cachedAuthor так и не совпал с _currentAuthor — поллинг ни разу не зафиксировал
            // воспроизведение для этой реплики), закрываем её немедленно.
            // Раньше тут было ничего, и при отсутствии voice + отсутствии следующего PrintStarted
            // (конец диалога, пауза на выбор, простой) OnVoiceStop не эмитился вовсе — подписчики
            // залипали в состоянии «говорит».
            //
            // Если voice играет (_cachedAuthor == _currentAuthor), оставляем закрытие через поллинг:
            // voice имеет полное право продолжаться после окончания печати текста.
            if (_currentAuthor != null && _cachedAuthor != _currentAuthor)
            {
                var prev = _currentAuthor;
                _currentAuthor = null;
                OnVoiceStop?.Invoke(prev);
            }
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

            // Старый voice закончился или сменился. Если ТОТ ЖЕ говорун получил новый voice-клип —
            // это «тихий переход», аниматор не должен дёргаться: ни Stop, ни последующего Start
            // (Start не эмитится в HandlePrintStart при silent transition).
            if (oldPath != null && _cachedAuthor != null)
            {
                var silentTransition = newPath != null && _cachedAuthor == _currentAuthor;
                if (!silentTransition)
                {
                    var author = _cachedAuthor;
                    _cachedAuthor = null;
                    OnVoiceStop?.Invoke(author);
                }
                // В silent-кейсе _cachedAuthor оставляем как есть — он совпадёт с переприсваиванием ниже.
            }

            // Новый voice появился — назначаем его текущей активной реплике.
            if (newPath != null)
                _cachedAuthor = _currentAuthor;
        }
    }
}
