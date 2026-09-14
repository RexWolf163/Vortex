using System;
using System.Linq;
using UnityEngine;
using Vortex.Core.LoaderSystem.Bus;
using Vortex.Core.SaveSystem;
using Vortex.Core.SaveSystem.Bus;
using Vortex.Core.System.Abstractions;
using Vortex.Unity.AppSystem.System.TimeSystem;

namespace Vortex.Unity.SaveSystem.Drivers.GlobalPrefsDriver
{
    /// <summary>
    /// Драйвер глобального хранилища в PlayerPrefs. Основной контейнер — ключ <c>VortexGlobalSave</c>, копии —
    /// отдельные ключи; перечень копий хранится в своём ключе (PlayerPrefs не перечисляет ключи).
    /// Атомарность записи ключа обеспечивает платформенная реализация PlayerPrefs.
    /// </summary>
    public sealed class GlobalPrefsDriver : Singleton<GlobalPrefsDriver>, IGlobalSaveDriver
    {
        private const string Key = "VortexGlobalSave";
        private const string CopyPrefix = Key + "_copy_";
        private const string CopyListKey = Key + "_copies";
        private const string Separator = ";";

        /// <summary>Готовность объявляет контроллер по завершении чтения — драйвер событие не поднимает.</summary>
        public event Action OnInit
        {
            add { }
            remove { }
        }

        /// <summary>
        /// Саморегистрация. Подключится только драйвер, указанный для GlobalSaveController в DriverConfig;
        /// принятый драйвер ставит контроллер в очередь загрузки.
        /// </summary>
        [RuntimeInitializeOnLoadMethod]
        private static void Run()
        {
            if (!GlobalSaveController.SetDriver(Instance))
            {
                Dispose();
                return;
            }

            Loader.Register(GlobalSaveController.Instance);
        }

        public void Init()
        {
        }

        public void Destroy()
        {
        }

        public GlobalReadStatus Read(out string data)
        {
            data = null;
            if (!PlayerPrefs.HasKey(Key))
                return GlobalReadStatus.NoData;

            try
            {
                data = PlayerPrefs.GetString(Key);
                return GlobalReadStatus.Ok;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                return GlobalReadStatus.Error;
            }
        }

        public bool Write(string data) => SetAndSave(Key, data);

        public bool WriteCopy(string id, string data)
        {
            if (!SetAndSave(CopyPrefix + id, data))
                return false;

            var copies = GetCopies().ToList();
            if (!copies.Contains(id))
                copies.Add(id);
            return SetAndSave(CopyListKey, string.Join(Separator, copies));
        }

        public string[] GetCopies() =>
            PlayerPrefs.GetString(CopyListKey, string.Empty)
                .Split(new[] { Separator }, StringSplitOptions.RemoveEmptyEntries);

        public bool ReadCopy(string id, out string data)
        {
            data = null;
            var key = CopyPrefix + id;
            if (!PlayerPrefs.HasKey(key))
                return false;

            data = PlayerPrefs.GetString(key);
            return true;
        }

        public void DeleteCopy(string id)
        {
            PlayerPrefs.DeleteKey(CopyPrefix + id);
            SetAndSave(CopyListKey, string.Join(Separator, GetCopies().Where(c => c != id)));
        }

        public void ScheduleFlush(Action flush) => TimeController.Call(flush, this);

        private static bool SetAndSave(string key, string data)
        {
            try
            {
                PlayerPrefs.SetString(key, data ?? string.Empty);
                PlayerPrefs.Save();
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[GlobalPrefsDriver] Ошибка записи ключа {key}");
                Debug.LogException(e);
                return false;
            }
        }
    }
}
