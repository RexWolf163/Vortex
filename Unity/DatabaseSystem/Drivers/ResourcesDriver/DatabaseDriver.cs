using System;
using System.Collections.Generic;
using UnityEngine;
using Vortex.Core.DatabaseSystem;
using Vortex.Core.DatabaseSystem.Model;
using Vortex.Core.SettingsSystem.Bus;
using Vortex.Core.System.Abstractions;
using Vortex.Unity.DatabaseSystem.Presets;

namespace Vortex.Unity.DatabaseSystem.Drivers.ResourcesDriver
{
    public partial class DatabaseDriver : Singleton<DatabaseDriver>, IDriver
    {
        public event Action OnInit;

        private static void CallOnInit() => Instance.OnInit?.Invoke();

        /// <summary>
        /// Инициализация
        /// Запускается автоматически после назначения драйвера системе
        /// </summary>
        public void Init()
        {
            //OnInit вызывается после завершения асинхронной загрузки данных
        }

        /// <summary>
        /// Передача указателя на реестр БД в драйвер для заполнения
        /// </summary>
        /// <param name="records"></param>
        /// <param name="uniqRecords"></param>
        public void SetIndex(Dictionary<string, Record> records, HashSet<string> uniqRecords) =>
            DatabaseDriverBase.SetIndex(records, uniqRecords);

        /// <summary>
        /// Возвращает новый экземпляр записи заполненной по пресету с указанным id
        /// </summary>
        /// <param name="guid"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public T GetNewRecord<T>(string guid) where T : Record, new() => DatabaseDriverBase.GetNewRecord<T>(guid);

        /// <summary>
        /// Возвращает новые экземпляры для всех multyinstance пресетов в БД
        /// чьи модели отвечают указанному типу
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public T[] GetNewRecords<T>() where T : Record, new() => DatabaseDriverBase.GetNewRecords<T>();

        /// <summary>
        /// Проверяет соответствие пресета указанному типу
        /// </summary>
        /// <param name="guid"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public bool CheckPresetType<T>(string guid) where T : Record => DatabaseDriverBase.CheckPresetType<T>(guid);

        public void Destroy()
        {
            if (Settings.Data().DebugMode)
                Debug.LogError("DatabaseDriver is destroyed");

            DatabaseDriverBase.Clean();
        }

        /// <summary>
        /// Добавление записи в БД
        /// </summary>
        /// <param name="record"></param>
        /// <param name="data"></param>
        private static void AddRecord(Record record, IRecordPreset data) => DatabaseDriverBase.AddRecord(record, data);
    }
}