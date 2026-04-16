using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Vortex.Unity.AppSystem.System.TimeSystem
{
    /// <summary>
    /// Класс таймера.
    /// Центральный диспетчер для вызова экшенов по времени
    /// </summary>
    public class TimeController : MonoBehaviour
    {
        #region Events

        /// <summary>
        /// Синхронизатор времени для дебага или иного чего 
        /// </summary>
        public static event Action TimeSync;

        #endregion

        #region Params

        /// <summary>
        /// Тиков в секунду
        /// </summary>
        internal const double TicksPerSecond = 10000000;

        /// <summary>
        /// Шаг проверки очереди экшенов.
        /// Чтобы разгрузить проц от проверки на каждом кадре
        /// </summary>
        private const float StepTime = 0.1f;

        /// <summary>
        /// Отметка времени последней проверки очереди 
        /// </summary>
        private static double _lastCheckTime = -1;

        // Переиспользуемый буффер, избавляемся от пересоздания списков
        private static readonly List<Action> ReadyQueue = new();

        // Переиспользуемый буффер, избавляемся от пересоздания списков
        private static readonly List<object> RemoveBuffer = new();

        // Переиспользуемый буффер, избавляемся от пересоздания списков
        private static readonly List<int> RemoveIndices = new();

        /// <summary>
        /// Очередь "следующей волны"
        /// Используется для экшенов, которые откладываются через Accumulate
        /// </summary>
        private static readonly Dictionary<object, Action> NextWaveQueue = new();

        /// <summary>
        /// Очередь на срабатывание
        /// </summary>
        [ShowInInspector, HideInEditorMode] private static List<QueuedAction> _anonymousQueue = new();

        /// <summary>
        /// Очередь на срабатывание без указанного владельца
        /// </summary>
        [ShowInInspector, HideInEditorMode] private static Dictionary<object, QueuedAction> _queue = new();

        /// <summary>
        /// Следующий таймер для обработки
        /// </summary>
        private static double _nextTimer = double.MaxValue;

        /// <summary>
        /// Индекс экшенов для запуска в FixUpdate
        /// </summary>
        private static readonly List<Action> FixUpdateIndex = new();

        #endregion

        #region Public

        /// <summary>
        /// Текущая дата.
        /// Кешируем, чтобы не поменялась на протяжении кадра
        /// (на всякий случай)
        /// </summary>
        public static DateTime Date { get; private set; }

        /// <summary>
        /// Текущее время в секундах.
        /// Два знака после запятой
        /// </summary>
        public static double Time { get; private set; }

        /// <summary>
        /// Отметка времени приложения
        /// UNIX время
        /// </summary>
        public static long Timestamp
        {
            get
            {
                if (Date.Year <= 1)
                    return 0;
                return new DateTimeOffset(Date).ToUnixTimeMilliseconds();
            }
        }

        /// <summary>
        /// Отложенный на конец кадра вызов экшена
        /// </summary>
        /// <param name="action">Отложенный экшен</param>
        public static void Call(Action action) => CallInternal(action, 0, null);

        /// <summary>
        /// Отложенный на конец кадра вызов экшена
        /// </summary>
        /// <param name="action">Отложенный экшен</param>
        /// <param name="owner">
        /// Владелец запроса. Если null, экшен будет без владельца и не может быть отменен.
        /// Если указан владелец - все предыдущие вызовы того же владельца будут перезаписаны.
        /// </param>
        public static void Call<T>(Action action, T owner) where T : class
            => CallInternal(action, 0, owner);

        /// <summary>
        /// Отложенный вызов экшена
        /// </summary>
        /// <param name="action">Отложенный экшен</param>
        /// <param name="stepSecs">Задержка в секундах</param>
        public static void Call(Action action, float stepSecs) => CallInternal(action, stepSecs, null);

        /// <summary>
        /// Отложенный на конец кадра вызов экшена
        /// </summary>
        /// <param name="action">Отложенный экшен</param>
        /// <param name="stepSecs">Задержка в секундах</param>
        /// <param name="owner">
        /// Владелец запроса. Если null, экшен будет без владельца и не может быть отменен.
        /// Если указан владелец - все предыдущие вызовы того же владельца будут перезаписаны.
        /// </param>
        public static void Call<T>(Action action, float stepSecs, T owner) where T : class
            => CallInternal(action, stepSecs, owner);

        /// <summary>
        /// Аккумулировать однотипные вызовы на "следующую волну" 
        /// </summary>
        /// <param name="action"></param>
        /// <param name="owner"></param>
        public static void Accumulate<T>(Action action, T owner) where T : class
        {
            if (NextWaveQueue.TryAdd(owner, action)) return;
            NextWaveQueue[owner] = action;
        }

        /// <summary>
        /// Удалить из очереди экшен указанного владельца
        /// </summary>
        /// <param name="owner">Владелец запроса</param>
        public static void RemoveCall<T>(T owner) where T : class
        {
            _queue.Remove(owner);
            NextWaveQueue.Remove(owner);
        }

        /// <summary>
        /// Преобразует секунды в DateTime в локальном часовом поясе
        /// </summary>
        /// <param name="seconds">Отметка времени в формате приложения (секунды)</param>
        public static DateTime DateFromSeconds(long seconds)
        {
            // Unix-время отсчитывается с 1 января 1970 года (эпоха Unix)
            var epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

            // Добавляем количество секунд к эпохе и конвертируем в локальное время
            var dateTime = epoch.AddSeconds(seconds);

            // Возвращаем время, скорректированное для локальной часовой зоны
            return TimeZoneInfo.ConvertTimeFromUtc(dateTime, TimeZoneInfo.Local);
        }

        /// <summary>
        /// Преобразует тики в DateTime в локальном часовом поясе
        /// </summary>
        /// <param name="ticks">сколько тиков</param>
        public static DateTime DateFromTicks(long ticks)
        {
            var time = new DateTime(ticks);
            return TimeZoneInfo.ConvertTimeFromUtc(time, TimeZoneInfo.Local);
        }

        #region FixUpdate

        /// <summary>
        /// Добавить колбэк для вызова каждый кадр на FixUpdate
        /// </summary>
        /// <param name="callback"></param>
        public static void AddCallback(Action callback) => FixUpdateIndex.Add(callback);

        /// <summary>
        /// Убрать колбэк из списка вызоваемых каждый кадр на FixUpdate
        /// </summary>
        /// <param name="callback"></param>
        public static void RemoveCallback(Action callback) => FixUpdateIndex.Remove(callback);

        #endregion

        #endregion

        #region Private

        [RuntimeInitializeOnLoadMethod]
        private static void AutoCreate()
        {
            var go = new GameObject("TimeController");
            go.AddComponent<TimeController>();

            SetTimeValue();
        }

        /// <summary>
        /// Отложенный вызов экшена
        /// </summary>
        /// <param name="action">Отложенный экшен</param>
        /// <param name="stepSecs">Через сколько секунд вызвать</param>
        /// <param name="owner">
        /// Владелец запроса. Если null, экшен будет без владельца и не может быть отменен.
        /// Если указан владелец - все предыдущие вызовы того же владельца будут перезаписаны.
        /// </param>
        private static void CallInternal(Action action, float stepSecs = 0, object owner = null)
        {
            if (action == null)
            {
                if (owner != null && _queue.ContainsKey(owner))
                    _queue.Remove(owner);
                return;
            }

            if (stepSecs <= 0f)
                _lastCheckTime = Time - StepTime;

            _nextTimer = Math.Min(_nextTimer, Time + stepSecs);
            var triggerTime = Time + stepSecs;

            if (owner == null)
            {
                // удалено .Clone()
                // делегаты в C# неизменяемы, Clone() создаёт лишние вызовы
                _anonymousQueue.Add(new QueuedAction
                {
                    Owner = null,
                    Action = action,
                    Timestamp = triggerTime
                });
                return;
            }

            _queue[owner] = new QueuedAction
            {
                Owner = owner,
                Action = action,
                Timestamp = triggerTime
            };
        }

        private static void SetTimeValue()
        {
            var now = DateTime.UtcNow;
            Date = now;
            Time = Math.Round(now.Ticks / TicksPerSecond, 2);
        }

        private void Awake() => DontDestroyOnLoad(this);

        /// <summary>
        /// Проверка очереди запросов и активация тех, чье время пришло
        /// </summary>
        private void CheckQueue()
        {
            if (_anonymousQueue.Count == 0 && _queue.Count == 0) return;
            if (Time < _nextTimer)
                return;
            _nextTimer = double.MaxValue;

            // Удалены временные списки и пересоздание списков
            // Меньше нагрузка на GC

            ReadyQueue.Clear();

            // Anonymous queue — FIFO порядок сохранён
            RemoveIndices.Clear();
            for (var i = 0; i < _anonymousQueue.Count; i++)
            {
                var actionData = _anonymousQueue[i];
                if (actionData.Timestamp <= Time)
                {
                    ReadyQueue.Add(actionData.Action);
                    RemoveIndices.Add(i);
                }
                else
                {
                    _nextTimer = Math.Min(_nextTimer, actionData.Timestamp);
                }
            }

            // Удаляем с конца — индексы не сдвигаются
            for (var i = RemoveIndices.Count - 1; i >= 0; i--)
                _anonymousQueue.RemoveAt(RemoveIndices[i]);

            using var enumerator = _queue.GetEnumerator();
            RemoveBuffer.Clear();

            while (enumerator.MoveNext())
            {
                var (key, actionData) = enumerator.Current;
                if (actionData.Timestamp <= Time)
                {
                    //Очистка залагавших удаленных объектов
                    if (key != null)
                        ReadyQueue.Add(actionData.Action);
                    RemoveBuffer.Add(key);
                }
                else
                {
                    _nextTimer = Math.Min(_nextTimer, actionData.Timestamp);
                }
            }

            foreach (var key in RemoveBuffer)
                _queue.Remove(key);


            foreach (var action in ReadyQueue)
            {
                try
                {
                    action?.Invoke();
                }
                catch (Exception ex)
                {
                    Debug.LogError(ex);
                }
            }
        }

        /// <summary>
        /// Сигнал синхронизации кадра
        /// </summary>
        private void Update() => TimeSync?.Invoke();

        /// <summary>
        /// Обновляем данные времени и запускаем проверку очереди,
        /// если с последней проверки прошло больше или равно шагу проверки
        /// </summary>
        private void LateUpdate()
        {
            SetTimeValue();
            //Запуск отложенной волны, если корректный ее запуск пропущен
            if (NextWaveQueue.Count > 0)
                RunNextWave();
            if (Time - _lastCheckTime < StepTime)
                return;
            _lastCheckTime = Time;
            CheckQueue();
        }

        /// <summary>
        /// Каждый кадр вызываем зарегистрированные колбэки
        /// </summary>
        private void FixedUpdate()
        {
            foreach (var action in FixUpdateIndex)
            {
                try
                {
                    action?.Invoke();
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                }
            }
        }

        /// <summary>
        /// Запуск экшенов "следующей волны", отложенных через Accumulate
        /// </summary>
        private static void RunNextWave()
        {
            if (NextWaveQueue.Count == 0)
                return;

            ReadyQueue.Clear();
            ReadyQueue.AddRange(NextWaveQueue.Values);
            NextWaveQueue.Clear();

            foreach (var action in ReadyQueue)
            {
                try
                {
                    action?.Invoke();
                }
                catch (Exception ex)
                {
                    Debug.LogError(ex);
                }
            }
        }

        #endregion
    }
}