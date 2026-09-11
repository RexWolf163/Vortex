using System;
using UnityEngine;
using Vortex.Core.System.Abstractions;
using Vortex.Sdk.RebindSystem.Bus;

namespace Vortex.Sdk.RebindSystem.Drivers
{
    /// <summary>
    /// Драйвер хранения снимка переназначений в PlayerPrefs. Копии нечитаемых снимков — отдельными ключами
    /// с отметкой времени.
    /// </summary>
    public sealed class RebindPlayerPrefsDriver : Singleton<RebindPlayerPrefsDriver>, IRebindStorageDriver
    {
        private const string Key = "VortexRebindSnapshot";

        public event Action OnInit;

        /// <summary>
        /// Саморегистрация в шину. Подключится только драйвер, указанный для RebindBus в DriverConfig, —
        /// остальных отклонит белый список.
        /// </summary>
        [RuntimeInitializeOnLoadMethod]
        private static void Bootstrap() => RebindBus.SetDriver(Instance);

        public void Init() => OnInit?.Invoke();

        public void Destroy()
        {
        }

        public StorageReadStatus Load(out string data)
        {
            data = null;
            if (!PlayerPrefs.HasKey(Key))
                return StorageReadStatus.NoData;

            try
            {
                data = PlayerPrefs.GetString(Key);
                return StorageReadStatus.Ok;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                return StorageReadStatus.Error;
            }
        }

        public bool Save(string data) => Write(Key, data);

        public bool Backup(string data, string stamp) => Write($"{Key}_backup_{stamp}", data);

        private static bool Write(string key, string data)
        {
            try
            {
                PlayerPrefs.SetString(key, data ?? string.Empty);
                PlayerPrefs.Save();
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[RebindPlayerPrefsDriver] Ошибка записи ключа {key}");
                Debug.LogException(e);
                return false;
            }
        }
    }
}
