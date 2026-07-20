using System;
using System.Globalization;
using UnityEngine;
using Vortex.Core.AppSystem.Bus;
using Vortex.Core.System.Enums;
using Vortex.Unity.AppSystem.System.TimeSystem;

namespace Vortex.Sdk.Core.GameCore
{
    /// <summary>
    /// Учёт двух независимых таймингов.
    ///
    /// 1. Время прохождения — растёт строго пока игровое состояние == <see cref="GameStates.Play"/>.
    ///    Хранится в сейве (<see cref="GameTimeData.PlaySeconds"/>), при загрузке принимает значение слота.
    ///    By design: расфокус уводит игру в Paused (см. GameController.OnApplicationStateChanged),
    ///    и счёт прохождения стоит, пока игрок осознанно не снимет паузу. Автовозобновления
    ///    по возврату фокуса нет и не предполагается.
    /// 2. Время приложения — идёт непрерывно от перехода в <see cref="AppStates.Running"/> и до
    ///    завершения приложения. Расфокус НЕ останавливает счёт (но провоцирует запись —
    ///    ОС может убить свёрнутое приложение без Stopping). Хранится в PlayerPrefs,
    ///    отпечаток кладётся в сейв.
    ///
    /// Учёт событийный: работы в кадре нет, накопление идёт на переходах состояний.
    /// Длительности считаются разностями <see cref="TimeController.Time"/> (это секунды от 0001-01-01,
    /// годятся только для дельт). Абсолютная метка берётся из DateTimeOffset — использовать
    /// TimeController.Timestamp нельзя, он возвращает миллисекунды вопреки своему описанию.
    ///
    /// Осознанные архитектурные решения (не поднимать заново на ревью):
    /// 1. Прямая запись в PlayerPrefs вместо драйверной схемы — трейд-офф ради отказа от
    ///    оверинжиниринга: поднимать контроллер с драйвером ради одного long не окупается.
    /// 2. Счётчик приложения живёт здесь, а не в Core/AppSystem, хотя измеряет время приложения.
    ///    AppModel держит точку отсчёта системного времени — это инфраструктура. Здесь же —
    ///    данные аналитики, нужные только потребителям SDK.Game, то есть доменная забота слоя 3.
    ///    Перенос в ядро затащил бы игровую аналитику в платформонезависимый слой.
    /// </summary>
    public partial class GameController
    {
        private const string AppSecondsKey = "Vortex.GameTime.AppSeconds";
        private const float FlushStepSeconds = 60f;

        /// <summary>Владелец отложенного вызова flush в TimeController.</summary>
        private static readonly object TimeTrackingKey = new();

        #region RuntimeState

        /// <summary>Идёт ли сейчас отрезок прохождения.</summary>
        private static bool _inPlay;

        /// <summary>Метка открытия отрезка прохождения на оси TimeController.Time.</summary>
        private static double _playMark;

        /// <summary>
        /// Накопленное время прохождения с долями секунды. Источник истины — модель:
        /// синхронизируется с ней при открытии отрезка, что штатно покрывает загрузку слота.
        /// </summary>
        private static double _playAccum;

        /// <summary>Запущен ли учёт времени приложения.</summary>
        private static bool _appStarted;

        /// <summary>Метка открытия отрезка приложения на оси TimeController.Time.</summary>
        private static double _appMark;

        /// <summary>База, поднятая из PlayerPrefs на старте учёта.</summary>
        private static long _appBase;

        #endregion

        #region Public API

        /// <summary>
        /// Время текущего прохождения с учётом незавершённого отрезка.
        /// Вне игры (<see cref="GameStates.Off"/>, до первого NewGame, edit-mode) — <see cref="TimeSpan.Zero"/>.
        /// Время закрытого прохождения читается из данных слота, а не отсюда.
        /// </summary>
        public static TimeSpan PlayTime
        {
            get
            {
#if UNITY_EDITOR
                //Вне рантайма учёта нет, а GetData() в edit-mode пересоздаёт модель на каждое обращение
                if (!Application.isPlaying)
                    return TimeSpan.Zero;
#endif
                var state = GetState();
                if (state is GameStates.Off or GameStates.Loading)
                    return TimeSpan.Zero;

                var seconds = _playAccum;
                if (_inPlay)
                {
                    var delta = TimeController.Time - _playMark;
                    if (delta > 0)
                        seconds += delta;
                }

                return TimeSpan.FromSeconds((long)seconds);
            }
        }

        /// <summary>
        /// Суммарное время в приложении за все запуски, с учётом незавершённого отрезка.
        /// </summary>
        public static TimeSpan AppTime => TimeSpan.FromSeconds(CurrentAppSeconds());

        /// <summary>
        /// Дата начала текущего прохождения. <c>default</c>, если прохождение не начато.
        /// </summary>
        public static DateTime SessionStarted
        {
            get
            {
#if UNITY_EDITOR
                if (!Application.isPlaying)
                    return default;
#endif
                var data = Get<GameTimeData>();
                if (data == null || data.SessionStartedAt <= 0)
                    return default;
                return DateTimeOffset.FromUnixTimeSeconds(data.SessionStartedAt).LocalDateTime;
            }
        }

        #endregion

        #region Init

        [RuntimeInitializeOnLoadMethod]
        private static void InitTimeTracking()
        {
            //Сброс на случай рестарта без выгрузки домена
            _inPlay = false;
            _playMark = 0;
            _playAccum = 0;
            _appStarted = false;
            _appMark = 0;
            _appBase = 0;

            //Хвост flush-цепочки от прошлой сессии (рестарт без выгрузки домена)
            TimeController.RemoveCall(TimeTrackingKey);

            App.OnStateChanged -= OnAppStateForTime;
            App.OnStateChanged += OnAppStateForTime;

            OnGameStateChanged -= OnGameStateForTime;
            OnGameStateChanged += OnGameStateForTime;

            OnNewGame -= OnNewGameForTime;
            OnNewGame += OnNewGameForTime;

            OnLoadGame -= OnLoadGameForTime;
            OnLoadGame += OnLoadGameForTime;
        }

        #endregion

        #region Playthrough

        /// <summary>
        /// Событие не несёт параметров, а GetState() внутри обработчика возвращает уже НОВОЕ
        /// состояние — поэтому признак «был в Play» держим сами.
        /// </summary>
        private static void OnGameStateForTime()
        {
            var isPlay = GetState() == GameStates.Play;
            if (isPlay == _inPlay)
                return;

            if (isPlay)
            {
                OpenPlayInterval();
                return;
            }

            ClosePlayInterval();
        }

        private static void OpenPlayInterval()
        {
            //Ресинк только если модель реально разошлась (загрузка слота / новая игра).
            //Безусловное перечитывание из long обнуляло бы накопленные доли секунды —
            //систематический дрейф вниз на каждом цикле Play→Paused→Play.
            var data = Get<GameTimeData>();
            var stored = data?.PlaySeconds ?? 0;
            if (stored != (long)_playAccum)
                _playAccum = stored;

            _playMark = TimeController.Time;
            _inPlay = true;
        }

        private static void ClosePlayInterval()
        {
            if (!_inPlay)
                return;
            _inPlay = false;
            AccumulatePlay();
        }

        /// <summary>
        /// Дописывает прошедшее с метки и сдвигает метку. Повторный вызов не удваивает учёт.
        /// </summary>
        private static void AccumulatePlay()
        {
            var delta = TimeController.Time - _playMark;
            if (delta < 0)
                delta = 0; //Перевод системных часов назад
            _playMark = TimeController.Time;
            _playAccum += delta;

            var data = Get<GameTimeData>();
            if (data == null)
                return;
            data.PlaySeconds = (long)_playAccum;
        }

        /// <summary>
        /// Только фиксация даты начала прохождения. Накопитель и метку здесь трогать нельзя:
        /// событие приходит уже после входа в Play, то есть отрезок открыт, и вмешательство
        /// убило бы живой отрезок. Сброс накопителя обеспечивают Init() модели и ресинк
        /// в OpenPlayInterval.
        /// </summary>
        private static void OnNewGameForTime()
        {
            var data = Get<GameTimeData>();
            if (data == null)
                return;
            data.SessionStartedAt = UtcNowSeconds();
        }

        /// <inheritdoc cref="OnNewGameForTime"/>
        private static void OnLoadGameForTime()
        {
            var data = Get<GameTimeData>();
            if (data == null)
                return;
            //Сейв старого формата — считаем началом прохождения момент этой загрузки
            if (data.SessionStartedAt <= 0)
                data.SessionStartedAt = UtcNowSeconds();
        }

        #endregion

        #region Application

        private static void OnAppStateForTime(AppStates state)
        {
            switch (state)
            {
                case AppStates.Running:
                    StartAppTracking();
                    break;
                case AppStates.Unfocused:
                    //Счёт НЕ прерываем — метка не сдвигается (развилка 7 ТЗ).
                    //Но фиксируем: ОС может убить свёрнутое приложение, не прислав Stopping
                    FlushAppSeconds();
                    break;
                case AppStates.Stopping:
                    FlushAppSeconds();
                    break;
            }
        }

        /// <summary>
        /// Идемпотентно: Running приходит повторно (например, после возврата фокуса),
        /// и перезапись метки съедала бы уже отсчитанное время.
        /// Старт именно на Running гарантирует, что TimeController уже инициализирован.
        /// </summary>
        private static void StartAppTracking()
        {
            if (_appStarted)
                return;
            _appStarted = true;
            _appBase = ReadAppSeconds();
            _appMark = TimeController.Time;
            ScheduleFlush();
        }

        private static long CurrentAppSeconds()
        {
            //До старта учёта отдаём сохранённую базу, а не ноль: иначе сохранение,
            //случившееся раньше Running, затёрло бы корректный отпечаток нулём
            if (!_appStarted)
                return ReadAppSeconds();

            var delta = TimeController.Time - _appMark;
            if (delta < 0)
                delta = 0;
            return _appBase + (long)delta;
        }

        /// <summary>
        /// Пишет ПОЛНОЕ текущее значение, включая открытый отрезок, метку не сдвигая.
        /// Отрезок приложения штатно не закрывается до конца работы — запись только
        /// накопленного обесценила бы периодический flush.
        /// </summary>
        private static void FlushAppSeconds()
        {
            if (!_appStarted)
                return;
            PlayerPrefs.SetString(AppSecondsKey, CurrentAppSeconds().ToString(CultureInfo.InvariantCulture));
            PlayerPrefs.Save();
        }

        /// <summary>
        /// Невалидное значение (мусор, не-число, отрицательное) трактуется как 0.
        /// Осознанное отклонение от fail-fast: аналитический счётчик не должен ронять приложение.
        /// </summary>
        private static long ReadAppSeconds()
        {
            var raw = PlayerPrefs.GetString(AppSecondsKey, string.Empty);
            if (!long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) || value < 0)
                return 0;
            return value;
        }

        private static void ScheduleFlush() =>
            TimeController.Call(OnFlushTick, FlushStepSeconds, TimeTrackingKey);

        private static void OnFlushTick()
        {
            FlushAppSeconds();
            ScheduleFlush();
        }

        #endregion

        #region Save

        /// <summary>
        /// Фиксация таймингов перед сериализацией. Без неё сохранение из активной игры
        /// записало бы значение на момент входа в Play, а загрузка честно восстановила бы
        /// заниженное. Вызывается из GetSaveData.
        /// </summary>
        internal static void ActualizeTimeData()
        {
            if (_inPlay)
                AccumulatePlay();

            var data = Get<GameTimeData>();
            if (data == null)
                return;
            data.AppSecondsSnapshot = CurrentAppSeconds();
        }

        private static long UtcNowSeconds() => DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        #endregion
    }
}
