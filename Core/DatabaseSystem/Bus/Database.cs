using System;
using System.Collections.Generic;
using System.Linq;
using Vortex.Core.DatabaseSystem.Model;
using Vortex.Core.LoggerSystem.Bus;
using Vortex.Core.LoggerSystem.Model;
using Vortex.Core.SaveSystem.Bus;
using Vortex.Core.System.Abstractions;

namespace Vortex.Core.DatabaseSystem.Bus
{
    public partial class Database : SystemController<Database, IDriver>
    {
        /// <summary>
        /// Реестр синглтон-записей в БД 
        /// </summary>
        private Dictionary<string, Record> _singletonRecords = new();

        /// <summary>
        /// Список ключей записей, выдаваемых в виде новых записей с заполнением из пресета 
        /// </summary>
        private HashSet<string> _multiInstanceRecords = new();

        /// <summary>
        /// Возвращает запись из БД по GUID приведенная к указанному типа
        /// </summary>
        /// <param name="guid"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static T GetRecord<T>(string guid) where T : Record, new()
        {
            if (!Instance._singletonRecords.TryGetValue(guid, out var singletonRec))
            {
                if (Instance._multiInstanceRecords.Contains(guid))
                {
                    Log.Print(
                        new LogData(LogLevel.Error,
                            $"MultiInstance Record cannot asked as Singleton Record. GUID: {guid}", Instance));
                    return null;
                }

                Log.Print(
                    new LogData(LogLevel.Error, $"Record not found for GUID: {guid}", Instance));
                return null;
            }

            //var record = Instance._singletonRecords[guid] as T;
            var record = singletonRec as T;
            if (record == null)
                Log.Print(
                    new LogData(LogLevel.Error, $"Record «{typeof(T).Name}» not found for GUID: {guid}", Instance));
            return record;
        }

        /// <summary>
        /// Возвращает запись из БД по GUID приведенная к указанному типа
        /// </summary>
        /// <param name="guid"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static T GetNewRecord<T>(string guid) where T : Record, new()
        {
            var result = Driver.GetNewRecord<T>(guid);
            if (result == null && Instance._singletonRecords.ContainsKey(guid))
                Log.Print(
                    new LogData(LogLevel.Error,
                        $"Singleton Record cannot asked as MultiInstance Record. GUID: {guid}", Instance));
            return result;
        }

        /// <summary>
        /// Возвращает перечень ключей для мультиинстанс-записей
        /// с фильтром по типу модели
        /// </summary>
        /// <returns></returns>
        public static T[] GetNewRecords<T>() where T : Record, new() => Driver.GetNewRecords<T>();

        /// <summary>
        /// Возвращает все имеющиеся в реестре записи указанного типа
        /// </summary>
        /// <returns></returns>
        public static T[] GetRecords<T>() where T : Record
        {
            var list = GetRecords(typeof(T));
            var result = new T[list.Length];
            Array.Copy(list, result, result.Length);
            return result;
        }

        /// <summary>
        /// Возвращает все имеющиеся в реестре записи указанного типа
        /// </summary>
        /// <returns></returns>
        public static Record[] GetRecords(Type recordClass)
        {
            var list = Instance._singletonRecords.Values;
            var result = new List<Record>();
            foreach (var record in list)
            {
                var tmp = record;
                if (!recordClass.IsAssignableFrom(tmp.GetType()))
                    continue;
                result.Add(tmp);
            }

            return result.ToArray();
        }

        /// <summary>
        /// Возвращает все имеющиеся в реестре записи
        /// </summary>
        /// <returns></returns>
        public static Record[] GetRecords() => Instance._singletonRecords.Values.ToArray();

        /// <summary>
        /// Обработка подключения нового драйвера
        /// </summary>
        protected override void OnDriverConnect()
        {
            Driver.SetIndex(_singletonRecords, _multiInstanceRecords);
            SaveController.Register(this);
        }

        /// <summary>
        /// Обработка отключения нового драйвера
        /// </summary>
        protected override void OnDriverDisconnect()
        {
            SaveController.UnRegister(this);
        }

        /// <summary>
        /// Возвращает результат поиска записи в БД по GUID, с автовыбором типа записи
        /// Используется для тестирования корректности линка 
        /// </summary>
        /// <param name="guid"></param>
        /// <returns></returns>
        public static bool TestRecord(string guid)
        {
            if (Instance._multiInstanceRecords.Contains(guid))
                return true;

            if (Instance._singletonRecords.ContainsKey(guid))
                return true;

            Log.Print(
                new LogData(LogLevel.Error, $"Record not found for GUID: {guid}", Instance));
            return false;
        }

        /// <summary>
        /// Возвращает перечень ключей для мультиинстанс-записей
        /// </summary>
        /// <returns></returns>
        public static string[] GetMultiInstancePresets() => Instance._multiInstanceRecords.ToArray();

        /// <summary>
        /// Возвращает перечень ключей для мультиинстанс-записей указанного типа
        /// </summary>
        /// <returns></returns>
        public static string[] GetMultiInstancePresets<T>() where T : Record => Instance._multiInstanceRecords
            .Where(guid => Driver.CheckPresetType<T>(guid)).ToArray();
    }
}