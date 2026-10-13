using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Vortex.Core.AppSystem.Bus;
using Vortex.Core.LoaderSystem.Bus;
using Vortex.Core.System.ProcessInfo;
using Vortex.Unity.AppSystem.System.TimeSystem;

namespace Vortex.Unity.UI.VirtualCursorSystem.InputDrivers
{
    /// <summary>
    /// Загрузчик пакета ввода курсора как SDK-модуль. Регистрируется в <see cref="Loader"/> (порядок — через
    /// <see cref="WaitingFor"/>), в <see cref="RunAsync"/> грузит <see cref="InputDriverSet"/> из
    /// <c>Resources/Settings</c> (failfast: нет ассета / пустой список / ни одного драйвера под платформу — исключение),
    /// подключает драйверы под текущую платформу и заводит покадровый тик для тех, кому он нужен.
    ///
    /// Тик — самоперепланирующаяся петля через <see cref="TimeController.Accumulate"/> (без скрытого раннера):
    /// <c>try/catch/finally</c> гарантирует продолжение петли при сбое; на исключение конкретного драйвера
    /// лог пишется только на ПЕРВОЕ в серии, счётчик сбрасывается на первом успешном кадре (анти-спам).
    /// </summary>
    public class CursorInputLoader : IProcess
    {
        /// <summary>Путь <see cref="InputDriverSet"/> в Resources.</summary>
        private const string AssetPath = "Settings/InputDriverSet";

        /// <summary>Владелец вызова петли тика в <see cref="TimeController"/>.</summary>
        private static readonly object TickOwner = new();

        /// <summary>Экземпляр загрузчика (создаётся при первом обращении).</summary>
        private static CursorInputLoader _instance;

        /// <summary>Экземпляр загрузчика, регистрируемый в <see cref="Loader"/>.</summary>
        private static CursorInputLoader Instance => _instance ??= new CursorInputLoader();

        /// <summary>Подключённые драйверы, которым нужен покадровый тик.</summary>
        private readonly List<TickSlot> _ticking = new();

        /// <summary>Загруженный набор драйверов.</summary>
        private InputDriverSet _set;

        /// <summary>Описание процесса для экрана загрузки.</summary>
        private readonly ProcessData _process = new() { Name = "Cursor Input", Progress = 0, Size = 1 };

        /// <summary>Регистрация загрузчика в <see cref="Loader"/> на старте приложения.</summary>
        [RuntimeInitializeOnLoadMethod]
        private static void Register() => Loader.Register(Instance);

        /// <summary>
        /// Зависимостей нет. <c>InputController</c> инициализируется лениво по первому доступу и не является
        /// ISystemController, поэтому в WaitingFor его указать нельзя — и не нужно.
        /// </summary>
        public Type[] WaitingFor() => Type.EmptyTypes;

        /// <summary>Описание процесса для экрана загрузки.</summary>
        public ProcessData GetProcessInfo() => _process;

        /// <summary>
        /// Загрузить <see cref="InputDriverSet"/> (нет ассета / пустой список — исключение), подключить драйверы,
        /// поддерживающие текущую платформу (ни одного — исключение), запустить тик для нуждающихся и подписать
        /// <see cref="Teardown"/> на выход.
        /// </summary>
        public UniTask RunAsync(CancellationToken cancellationToken)
        {
            _set = Resources.Load<InputDriverSet>(AssetPath);
            if (_set == null)
                throw new InvalidOperationException(
                    "[CursorInput] USING_VORTEX_CURSOR включён, но ассет InputDriverSet отсутствует в Resources/Settings.");

            var drivers = _set.Drivers;
            if (drivers == null || drivers.Length == 0)
                throw new InvalidOperationException(
                    "[CursorInput] InputDriverSet пуст — не задан ни один InputDriver.");

            var platform = Application.platform;
            var connected = 0;
            foreach (var driver in drivers)
            {
                if (driver == null || !driver.SupportsPlatform(platform))
                    continue;
                driver.Connect();
                connected++;
                if (driver.NeedsTick)
                    _ticking.Add(new TickSlot(driver));
            }

            if (connected == 0)
                throw new InvalidOperationException(
                    $"[CursorInput] В InputDriverSet нет ни одного драйвера для платформы {platform}.");

            if (_ticking.Count > 0)
                TimeController.Accumulate(TickAll, TickOwner);

            App.OnExit += Teardown;
            _process.Progress = 1;
            return UniTask.CompletedTask;
        }

        /// <summary>
        /// Покадровая петля тика. Внутренний try/catch изолирует сбойный драйвер, finally гарантирует,
        /// что петля переживёт любой бросок и перепланируется на следующий кадр.
        /// </summary>
        private void TickAll()
        {
            try
            {
                var dt = Time.unscaledDeltaTime;
                for (var i = 0; i < _ticking.Count; i++)
                {
                    var slot = _ticking[i];
                    try
                    {
                        slot.Driver.Tick(dt);
                        slot.FailStreak = 0;          // кадр без ошибки — снимаем серию (перевзводим лог)
                    }
                    catch (Exception e)
                    {
                        if (slot.FailStreak++ == 0)    // лог только на первое исключение серии (анти-спам)
                            Debug.LogException(e);
                    }
                }
            }
            finally
            {
                TimeController.Accumulate(TickAll, TickOwner);
            }
        }

        /// <summary>Выход из приложения: остановить тик и отключить все драйверы набора.</summary>
        private void Teardown()
        {
            App.OnExit -= Teardown;
            TimeController.RemoveCall(TickOwner);
            if (_set?.Drivers != null)
                foreach (var driver in _set.Drivers)
                    driver?.Disconnect();
            _ticking.Clear();
        }

        /// <summary>Тикающий драйвер и счётчик подряд идущих сбоев (для анти-спама лога).</summary>
        private sealed class TickSlot
        {
            /// <summary>Драйвер с <see cref="InputDriver.NeedsTick"/>.</summary>
            public readonly InputDriver Driver;

            /// <summary>Число подряд идущих кадров с исключением; 0 — последний кадр прошёл без ошибки.</summary>
            public int FailStreak;

            /// <summary>Слот для драйвера.</summary>
            public TickSlot(InputDriver driver) => Driver = driver;
        }
    }
}
