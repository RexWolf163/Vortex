using System;
using System.Collections.Generic;
using Vortex.Core.Extensions.LogicExtensions.Actions;
using Vortex.Core.System.Abstractions;
using Vortex.Sdk.RebindSystem.Model;

namespace Vortex.Sdk.RebindSystem.Bus
{
    /// <summary>
    /// Шина системы переназначения клавиш: точка доступа к контроллеру и модели, события системы.
    ///
    /// Наследует <see cref="SystemController{T,TD}"/>, а не является статическим классом: драйвер хранения
    /// (PlayerPrefs / файл) выбирается в <c>DriverConfig</c> и проходит белый список <c>DriversGenericList</c>.
    /// Бизнес-операций шина не содержит — только доступ, события и внутренний канал к драйверу для контроллера.
    /// </summary>
    public class RebindBus : SystemController<RebindBus, IRebindStorageDriver>
    {
        /// <summary>Шлюз готовности: подписки выполнятся после загрузки системы (снимок применён, модель собрана).</summary>
        public static InitValve OnReady { get; } = InitValve.Create(out OnReadyOpen);

        private static readonly Action OnReadyOpen;

        /// <summary>Контроллер системы.</summary>
        public static IRebindController Controller { get; private set; }

        /// <summary>Модель состояния системы. <c>null</c> до загрузки.</summary>
        public static RebindModel Data => Controller?.Model;

        /// <summary>Система загружена и готова к работе.</summary>
        public static bool IsReady => Controller is { IsInitialized: true };

        /// <summary>
        /// Изменились слоты: значение или состояние конфликта. Одно событие на операцию; аргумент — адреса
        /// изменённых слотов.
        /// </summary>
        public static event Action<IReadOnlyList<string>> OnSlotsChanged;

        /// <summary>Состояние пересобрано целиком: загрузка, сброс карты или всех изменений, импорт.</summary>
        public static event Action OnRebuilt;

        /// <summary>Сменилась активность групп устройств (переключение раскладки).</summary>
        public static event Action OnGroupsChanged;

        /// <summary>Клапан перехвата. <c>null</c> до привязки контроллера.</summary>
        public static CaptureValve Capture => Controller?.Capture;

        /// <summary>Изменения сохраняются. <c>false</c> — только в памяти: нет драйвера или хранилище не прочиталось.</summary>
        public static bool CanPersist => Controller is { CanPersist: true };

        /// <summary>
        /// Сменилось состояние клапана перехвата: открытие, удерживаемые модификаторы, отказ при открытом клапане,
        /// закрытие с итогом в <see cref="CaptureValve.LastResult"/>.
        /// </summary>
        public static event Action<CaptureValve> OnCaptureChanged;

        /// <summary>Привязка контроллера. Вызывается контроллером при старте приложения.</summary>
        internal static void Bind(IRebindController controller) => Controller = controller;

        /// <summary>Открыть шлюз готовности. Вызывается контроллером по завершении загрузки.</summary>
        internal static void NotifyReady() => OnReadyOpen?.Invoke();

        internal static void RaiseSlotsChanged(IReadOnlyList<string> bindKeys)
        {
            if (bindKeys != null && bindKeys.Count > 0)
                OnSlotsChanged?.Invoke(bindKeys);
        }

        internal static void RaiseRebuilt() => OnRebuilt?.Invoke();

        internal static void RaiseGroupsChanged() => OnGroupsChanged?.Invoke();

        internal static void RaiseCaptureChanged(CaptureValve valve) => OnCaptureChanged?.Invoke(valve);

        // ── Канал к драйверу хранения (только для контроллера) ─────────────────────────────────────

        /// <summary>Прочитать снимок. Драйвер не подключён — <see cref="StorageReadStatus.Error"/>.</summary>
        internal static StorageReadStatus LoadSnapshot(out string data)
        {
            if (HasDriver())
                return Driver.Load(out data);

            data = null;
            return StorageReadStatus.Error;
        }

        /// <summary>Записать снимок. Драйвер не подключён — <c>false</c>.</summary>
        internal static bool SaveSnapshot(string data) => HasDriver() && Driver.Save(data);

        /// <summary>Сохранить копию нечитаемого снимка. Драйвер не подключён — <c>false</c>.</summary>
        internal static bool BackupSnapshot(string data, string stamp) => HasDriver() && Driver.Backup(data, stamp);

        protected override void OnDriverConnect()
        {
        }

        protected override void OnDriverDisconnect()
        {
        }
    }
}
