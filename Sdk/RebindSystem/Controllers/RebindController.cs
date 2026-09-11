using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Vortex.Core.LoaderSystem.Bus;
using Vortex.Core.System.Abstractions;
using Vortex.Core.System.ProcessInfo;
using Vortex.Sdk.RebindSystem.Bus;
using Vortex.Sdk.RebindSystem.Model;
using Vortex.Sdk.RebindSystem.Presets;

namespace Vortex.Sdk.RebindSystem.Controllers
{
    /// <summary>
    /// Контроллер системы переназначения клавиш — единственное место изменения состояния системы.
    /// Разбит на partial-файлы по темам; этот — жизненный цикл.
    ///
    /// Загружается процессом <see cref="Loader"/> без зависимостей: ассет ввода доступен с
    /// <c>RuntimeInitializeOnLoadMethod</c>, настройки — с <c>WaitSettings</c>, драйверы хранения
    /// подключаются сами. Хэндлеры ввода подписываются на <c>AppStates.Running</c>, то есть после загрузки, —
    /// к первому игровому нажатию переназначения уже применены. Процессы, которым нужны применённые
    /// переназначения ещё на этапе загрузки, объявляют зависимость от этой системы сами.
    /// </summary>
    public sealed partial class RebindController : Singleton<RebindController>, IRebindController, IProcess
    {
        private readonly ProcessData _processData = new("Rebind");

        public bool IsInitialized { get; private set; }

        public RebindModel Model { get; private set; }

        /// <summary>Настройки системы. <c>null</c> до загрузки или если ассета нет.</summary>
        internal RebindSettings Settings { get; private set; }

        [RuntimeInitializeOnLoadMethod]
        private static void Bootstrap()
        {
            RebindBus.Bind(Instance);
            Loader.Register(Instance);
        }

        public ProcessData GetProcessInfo() => _processData;

        public Type[] WaitingFor() => null;

        public async UniTask RunAsync(CancellationToken cancellationToken)
        {
            // Модель и ассет собираются с чистого листа. Шлюз OnReady статический и не переоткрывается —
            // как у остальных шин Vortex.
            IsInitialized = false;

            Settings = RebindSettings.Find();
            if (Settings == null)
                Debug.LogError("[RebindController] Не найден ассет RebindSettings в Resources — " +
                               "проверь Tools/Vortex/Debug/Check Core Assets.");

            if (!RebindBus.HasDriver())
                Debug.LogError("[RebindController] Драйвер хранения не подключён (DriverConfig → RebindBus). " +
                               "Переназначения действуют только до перезапуска.");

            BuildModel();
            LoadSnapshot();
            RebuildIndex();
            ApplyAllToAsset();

            IsInitialized = true;
            RebindBus.NotifyReady();
            RebindBus.RaiseRebuilt();
            await UniTask.CompletedTask;
        }
    }
}
