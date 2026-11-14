using System;
using System.Collections.Generic;
using UnityEngine;
using Vortex.Core.Extensions.ReactiveValues;
using Vortex.Unity.AppSystem.System.TimeSystem;
using Vortex.Unity.CutsceneSystem.Models;
using Object = UnityEngine.Object;

namespace Vortex.Unity.CutsceneSystem.Bus
{
    /// <summary>
    /// Шина катсцен: единая точка обращений. Вызывающему не нужно знать, какая катсцена идёт и чем она
    /// показана.
    ///
    /// Шина ничего не решает и ничего не исполняет. Обращение (открыть, дальше, пропустить, сигнал звена
    /// цепочки) она сразу передаёт держателю слота событием и забывает о нём. Всё остальное — проверки,
    /// загрузка, запуск цепочки, закрытие — делает держатель слота, оркестратор сцены. Шина о нём знает
    /// только то, что слот занят.
    ///
    /// Кроме слота шина хранит данные показа, которые читают звенья, условия и контент: номер фазы, признак
    /// пролёта и идентификатор цепочки идущего показа. Пишет их держатель слота
    /// </summary>
    public static class CutsceneBus
    {
        private static readonly object Key = new();
        private static readonly IntData PhaseData = new(0, Key);

        /// <summary>
        /// Владелец отложенной рассылки отказов
        /// </summary>
        private static readonly object RefusedKey = new();

        /// <summary>
        /// Ключи открытий, отклонённых из-за пустого слота. Их «показ завершён» уходит в конце кадра
        /// </summary>
        private static readonly List<string> Refused = new();

        /// <summary>
        /// Держатель слота. Шина не знает, кто это: ей нужен только факт, что слот занят
        /// </summary>
        private static Object _holder;

        /// <summary>
        /// Идентификатор цепочки идущего показа в реестре логических цепочек. По нему условия узнают свой
        /// показ. <c>null</c> — цепочка не запущена
        /// </summary>
        private static string _chain;

        /// <summary>
        /// Сколько принятых открытий ещё не получили свой «показ завершён»
        /// </summary>
        private static int _opened;

        /// <summary>
        /// Управляющие данные показа — их получает контент катсцены
        /// </summary>
        public static CutsceneControl Control { get; } = new(PhaseData);

        /// <summary>
        /// Идёт пролёт до конца цепочки
        /// </summary>
        public static bool IsSkipping { get; private set; }

        /// <summary>
        /// Катсцена открыта: открытие принято, контент загружается либо показ идёт
        /// </summary>
        public static bool IsActive => _opened > 0;

        /// <summary>
        /// Катсцена открывается: открытие принято оркестратором, началась загрузка контента. Параметр — ключ
        /// катсцены. За этим событием всегда приходит <see cref="OnShowFinished"/> с тем же ключом;
        /// отклонённое открытие этого события не даёт
        /// </summary>
        public static event Action<string> OnShowOpening;

        /// <summary>
        /// Показ начался: контент загружен, цепочка запущена, первая фаза ушла контенту. Параметр — ключ
        /// катсцены
        /// </summary>
        public static event Action<string> OnShowStarted;

        /// <summary>
        /// Показ завершён либо не состоялся (отказ открытия, сбой загрузки, отмена). Параметр — ключ катсцены.
        /// На каждое обращение <see cref="Open"/> приходит один раз; исключение — состязательные обращения
        /// из обработчиков событий показа (см. «Ограничения» в README)
        /// </summary>
        public static event Action<string> OnShowFinished;

        // ── Обращения — слушает держатель слота ───────────────────────────────

        internal static event Action<string> OnOpenRequested;
        internal static event Action OnNextRequested;
        internal static event Action<int> OnAutoNextRequested;
        internal static event Action OnSkipRequested;

        /// <summary>
        /// Звено «Фаза» отработало. Параметр — идентификатор цепочки звена
        /// </summary>
        internal static event Action<string> OnPhaseEntered;

        /// <summary>
        /// Звено «Завершение» отработало. Параметр — идентификатор цепочки звена
        /// </summary>
        internal static event Action<string> OnCompleted;

        /// <summary>
        /// Сигнал цепочке «можно дальше» — слушают условия
        /// </summary>
        internal static event Action OnNext;

        /// <summary>
        /// Сброс статики. До загрузки первой сцены: её оркестратор занимает слот на своём <c>OnEnable</c>,
        /// и сброс после него стёр бы регистрацию
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Init()
        {
            Refused.Clear();
            _holder = null;
            _chain = null;
            _opened = 0;
            IsSkipping = false;

            //Подписчики прошлого запуска (вход в Play Mode без перезагрузки домена) новому запуску не нужны
            OnShowOpening = null;
            OnShowStarted = null;
            OnShowFinished = null;
            OnOpenRequested = null;
            OnNextRequested = null;
            OnAutoNextRequested = null;
            OnSkipRequested = null;
            OnPhaseEntered = null;
            OnCompleted = null;
            OnNext = null;
            PhaseData.Set(0, Key);
        }

        // ── Обращения ─────────────────────────────────────────────────────────

        /// <summary>
        /// Открыть катсцену. Ключ — идентификатор записи <c>LogicChain</c>. Идущая катсцена сначала пролетает
        /// до конца. Отказ (пустой ключ, нет оркестратора, ключа нет в его индексе, цепочка не проходит
        /// проверку) — ошибка в лог и <see cref="OnShowFinished"/> с этим ключом
        /// </summary>
        public static void Open(string key)
        {
            _opened++;
            if (_holder != null)
            {
                OnOpenRequested?.Invoke(key);
                return;
            }

            //Слот пуст — открывать некому: отказ сразу, оркестратора не ждём. «Показ завершён» уходит
            //в конце кадра, а не изнутри этого вызова: иначе подписчик, открывающий катсцену заново на
            //каждый отказ, ушёл бы в бесконечную рекурсию
            Debug.LogError($"[CutsceneBus] Нет оркестратора катсцен: '{key}' открыть некому.");
            Refused.Add(key);
            TimeController.Accumulate(FlushRefused, RefusedKey);
        }

        /// <summary>
        /// Команда «дальше»: переход к следующему этапу. Запрос, пришедший раньше заданной в оркестраторе
        /// задержки после входа в фазу, игнорируется (защита от дребезга)
        /// </summary>
        public static void Next() => OnNextRequested?.Invoke();

        /// <summary>
        /// Сигнал «дальше» от самого контента (фаза с автопереходом). Защиту от дребезга обходит.
        /// Контент называет фазу, которую показывает: сигнал от фазы, с которой цепочка уже ушла,
        /// игнорируется — иначе он проскочил бы следующую фазу
        /// </summary>
        /// <param name="phase">Номер фазы, которую показывает контент</param>
        public static void Next(int phase) => OnAutoNextRequested?.Invoke(phase);

        /// <summary>
        /// Пропустить катсцену: цепочка пролетает до завершающего этапа и заканчивается. Контент ещё
        /// загружается — загрузка отменяется. Катсцена не открыта — ничего не делает
        /// </summary>
        public static void Skip() => OnSkipRequested?.Invoke();

        /// <summary>
        /// Разослать «показ завершён» открытиям, отклонённым из-за пустого слота
        /// </summary>
        private static void FlushRefused()
        {
            var keys = Refused.ToArray();
            Refused.Clear();
            foreach (var key in keys)
                NotifyFinished(key);
        }

        // ── Слот ──────────────────────────────────────────────────────────────

        /// <summary>
        /// Занять слот шины. Слот один: пока он занят, второй претендент отклоняется
        /// </summary>
        internal static bool Register(Object holder)
        {
            if (_holder != null && _holder != holder)
            {
                Debug.LogError(
                    $"[CutsceneBus] Слот оркестратора катсцен уже занят ('{_holder.name}'): '{holder.name}' отклонён.",
                    holder);
                return false;
            }

            _holder = holder;
            return true;
        }

        /// <summary>
        /// Освободить слот шины
        /// </summary>
        internal static void Unregister(Object holder)
        {
            if (_holder == holder)
                _holder = null;
        }

        // ── Данные показа — пишет держатель слота ─────────────────────────────

        /// <summary>
        /// Цепочка идущего показа; <c>null</c> — показа нет
        /// </summary>
        internal static void SetChain(string chain) => _chain = chain;

        internal static void SetSkipping(bool skipping) => IsSkipping = skipping;

        /// <summary>
        /// Сообщить контенту номер фазы
        /// </summary>
        internal static void SetPhase(int phase) => PhaseData.Set(phase, Key);

        /// <summary>
        /// Подать цепочке сигнал «можно дальше»
        /// </summary>
        internal static void SignalNext() => OnNext?.Invoke();

        /// <summary>
        /// Сигнала «можно дальше» кто-то ждёт: цепочка стоит на условии
        /// </summary>
        internal static bool HasWaiter => OnNext != null;

        /// <summary>
        /// Разослать «катсцена открывается». Возвращает <c>true</c>, если хотя бы один подписчик бросил
        /// исключение
        /// </summary>
        internal static bool NotifyOpening(string key) => Raise(OnShowOpening, key);

        /// <summary>
        /// Разослать «показ начался». Возвращает <c>true</c>, если хотя бы один подписчик бросил исключение
        /// </summary>
        internal static bool NotifyStarted(string key) => Raise(OnShowStarted, key);

        /// <summary>
        /// Разослать «показ завершён». Возвращает <c>true</c>, если хотя бы один подписчик бросил исключение
        /// </summary>
        internal static bool NotifyFinished(string key)
        {
            if (_opened > 0)
                _opened--;

            return Raise(OnShowFinished, key);
        }

        /// <summary>
        /// Вызвать подписчиков по одному: исключение одного остальным не мешает
        /// </summary>
        private static bool Raise(Action<string> handlers, string key)
        {
            if (handlers == null)
                return false;

            var failed = false;
            foreach (var handler in handlers.GetInvocationList())
            {
                try
                {
                    ((Action<string>)handler)(key);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                    failed = true;
                }
            }

            return failed;
        }

        // ── Звенья и условия цепочки ──────────────────────────────────────────

        /// <summary>
        /// Цепочка с этим идентификатором — цепочка идущего показа
        /// </summary>
        internal static bool IsCurrent(string chain) => chain != null && chain == _chain;

        /// <summary>
        /// Звено «Фаза»: вход в следующую фазу
        /// </summary>
        internal static void EnterPhase(string chain) => OnPhaseEntered?.Invoke(chain);

        /// <summary>
        /// Звено «Завершение»: показ пора закрыть
        /// </summary>
        internal static void Complete(string chain) => OnCompleted?.Invoke(chain);
    }
}
