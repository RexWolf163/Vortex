#if USING_VORTEX_CURSOR
using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Vortex.Core.AppSystem.Bus;
using Vortex.Core.LoaderSystem.Bus;
using Vortex.Core.System.ProcessInfo;
using Vortex.Unity.AppSystem.System.TimeSystem;

namespace Vortex.Unity.UI.VirtualCursorSystem
{
    /// <summary>
    /// Загрузчик пакета ввода курсора как SDK-модуль. Регистрируется в <see cref="Loader"/> (порядок — через
    /// <see cref="WaitingFor"/>), в <see cref="RunAsync"/> грузит <see cref="InputDriverSet"/> из
    /// <c>Resources/Settings</c> (failfast: нет ассета / пустой список — исключение), подключает драйверы под
    /// текущую платформу и заводит покадровый тик для тех, кому он нужен.
    ///
    /// Тик — самоперепланирующаяся петля через <see cref="TimeController.Accumulate"/> (без скрытого раннера):
    /// <c>try/catch/finally</c> гарантирует продолжение петли при сбое; на исключение конкретного драйвера
    /// лог пишется только на ПЕРВОЕ в серии, счётчик сбрасывается на первом успешном кадре (анти-спам).
    /// </summary>
    public class CursorInputLoader : IProcess
    {
        private const string AssetPath = "Settings/InputDriverSet";
        private static readonly object TickOwner = new();

        private static CursorInputLoader _instance;
        private static CursorInputLoader Instance => _instance ??= new CursorInputLoader();

        private readonly List<TickSlot> _ticking = new();
        private InputDriverSet _set;
        private readonly ProcessData _process = new() { Name = "Cursor Input", Progress = 0, Size = 1 };

        [RuntimeInitializeOnLoadMethod]
        private static void Register() => Loader.Register(Instance);

        // InputController лениво инициализируется по первому доступу (GetAction) и не является ISystemController,
        // поэтому его в WaitingFor указать нельзя (Loader такую зависимость не резолвит) — и не нужно.
        // Появится реальная ISystemController/IProcess-зависимость — перечислить её здесь.
        public Type[] WaitingFor() => Type.EmptyTypes;

        public ProcessData GetProcessInfo() => _process;

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
            foreach (var driver in drivers)
            {
                if (driver == null || !driver.SupportsPlatform(platform))
                    continue;
                driver.Connect();
                if (driver.NeedsTick)
                    _ticking.Add(new TickSlot(driver));
            }

            if (_ticking.Count > 0)
                TimeController.Accumulate(TickAll, TickOwner);

            App.OnExit += Teardown;
            _process.Progress = 1;
            return UniTask.CompletedTask;
        }

        // Покадровая петля тика. Внутренний try/catch изолирует сбойный драйвер, finally гарантирует,
        // что петля переживёт любой бросок и перепланируется на следующую волну.
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

        private void Teardown()
        {
            App.OnExit -= Teardown;
            TimeController.RemoveCall(TickOwner);
            if (_set?.Drivers != null)
                foreach (var driver in _set.Drivers)
                    driver?.Disconnect();
            _ticking.Clear();
        }

        private sealed class TickSlot
        {
            public readonly InputDriver Driver;
            public int FailStreak;
            public TickSlot(InputDriver driver) => Driver = driver;
        }
    }
}
#endif
