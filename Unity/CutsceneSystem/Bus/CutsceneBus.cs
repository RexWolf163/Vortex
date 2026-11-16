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
    /// Шина ничего не решает и ничего не исполняет. Обращение (открыть, дальше, выставить узел, пропустить)
    /// она сразу передаёт держателю слота событием и забывает о нём. Всё остальное — проверки, загрузка, ход
    /// графа, закрытие — делает держатель слота, оркестратор сцены. Шина о нём знает только то, что слот занят.
    ///
    /// Кроме слота шина хранит управляющие данные показа: имя текущего узла графа. Пишет их держатель слота
    /// </summary>
    public static class CutsceneBus
    {
        private static readonly object Key = new();
        private static readonly StringData NodeData = new(string.Empty, Key);

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
        /// Сколько принятых открытий ещё не получили свой «показ завершён»
        /// </summary>
        private static int _opened;

        /// <summary>
        /// Управляющие данные показа: имя текущего узла графа катсцены
        /// </summary>
        public static CutsceneControl Control { get; } = new(NodeData);

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
        /// Показ начался: контент загружен, граф запущен и показывает первый узел. Параметр — ключ катсцены
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
        internal static event Action<string> OnAutoNextRequested;
        internal static event Action<string> OnNodeRequested;
        internal static event Action OnSkipRequested;

        /// <summary>
        /// Сброс статики. До загрузки первой сцены: её оркестратор занимает слот на своём <c>OnEnable</c>,
        /// и сброс после него стёр бы регистрацию
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Init()
        {
            Refused.Clear();
            _holder = null;
            _opened = 0;

            //Подписчики прошлого запуска (вход в Play Mode без перезагрузки домена) новому запуску не нужны
            OnShowOpening = null;
            OnShowStarted = null;
            OnShowFinished = null;
            OnOpenRequested = null;
            OnNextRequested = null;
            OnAutoNextRequested = null;
            OnNodeRequested = null;
            OnSkipRequested = null;
            NodeData.Set(string.Empty, Key);
        }

        // ── Обращения ─────────────────────────────────────────────────────────

        /// <summary>
        /// Открыть катсцену. Ключ — ключ записи в индексе оркестратора сцены. Идущая катсцена сначала
        /// закрывается. Отказ (пустой ключ, нет оркестратора, ключа нет в его индексе) — ошибка в лог
        /// и <see cref="OnShowFinished"/> с этим ключом
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
        /// Команда «дальше»: сигнал <c>Next</c> графу катсцены. Вход в конечный узел закрывает показ. Запрос,
        /// пришедший раньше заданной в оркестраторе задержки после входа в узел, игнорируется (защита от дребезга)
        /// </summary>
        public static void Next() => OnNextRequested?.Invoke();

        /// <summary>
        /// Сигнал «дальше» от самого контента (узел с автопереходом). Защиту от дребезга обходит.
        /// Контент называет узел, который показывает: сигнал от узла, с которого показ уже ушёл,
        /// игнорируется — иначе он проскочил бы следующий узел
        /// </summary>
        /// <param name="nodeId">Id узла, который показывает контент</param>
        public static void Next(string nodeId) => OnAutoNextRequested?.Invoke(nodeId);

        /// <summary>
        /// Выставить узел графа, минуя условия (<c>ForceNode</c>): по Id или имени. Неизвестный узел — ошибка
        /// в лог, узел не меняется. Пришло, пока контент грузится, — применяется при старте показа вместо
        /// стартового узла. Защиту от дребезга не проходит
        /// </summary>
        /// <param name="node">Id или имя узла</param>
        public static void SetNode(string node) => OnNodeRequested?.Invoke(node);

        /// <summary>
        /// Пропустить катсцену: показ закрывается сразу. Контент ещё загружается — загрузка отменяется.
        /// Катсцена не открыта — ничего не делает
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
        /// Сообщить имя текущего узла (пусто — показа нет)
        /// </summary>
        internal static void PublishNode(string node) => NodeData.Set(node ?? string.Empty, Key);

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
    }
}
