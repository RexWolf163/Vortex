using System;
using System.IO;
using UnityEngine;
using Vortex.Core.System.Abstractions;
using Vortex.Sdk.RebindSystem.Bus;
using Vortex.Sdk.RebindSystem.Presets;
using Vortex.Unity.FileSystem.Bus;

namespace Vortex.Sdk.RebindSystem.Drivers
{
    /// <summary>
    /// Драйвер хранения снимка переназначений в файле. Папка — из <see cref="RebindSettings"/> относительно
    /// корня данных приложения (<see cref="FileBus.GetAppPath"/>); по умолчанию рядом с папкой сохранений,
    /// чтобы файл уезжал в облако платформы вместе с сейвами.
    ///
    /// Запись атомарная: сначала во временный файл, затем подмена основного. Оборванная запись (выход,
    /// падение, отключение питания) оставляет прежний снимок целым — частичная порча снимка возникает
    /// главным образом именно так.
    /// </summary>
    public sealed class RebindFileDriver : Singleton<RebindFileDriver>, IRebindStorageDriver
    {
        private const string FileName = "bindings";
        private const string Extension = ".json";
        private const string TempExtension = ".tmp";

        private string _folder;

        public event Action OnInit;

        /// <summary>
        /// Саморегистрация в шину. Подключится только драйвер, указанный для RebindBus в DriverConfig, —
        /// остальных отклонит белый список.
        /// </summary>
        [RuntimeInitializeOnLoadMethod]
        private static void Bootstrap() => RebindBus.SetDriver(Instance);

        public void Init()
        {
            var relative = RebindSettings.Find()?.SnapshotFolder ?? string.Empty;
            _folder = Path.Combine(FileBus.GetAppPath(), relative);
            OnInit?.Invoke();
        }

        public void Destroy()
        {
        }

        private string MainPath => Path.Combine(_folder, FileName + Extension);

        public StorageReadStatus Load(out string data)
        {
            data = null;
            var path = MainPath;
            if (!File.Exists(path))
                return StorageReadStatus.NoData;

            try
            {
                data = File.ReadAllText(path);
                return StorageReadStatus.Ok;
            }
            catch (Exception e)
            {
                Debug.LogError($"[RebindFileDriver] Ошибка чтения {path}");
                Debug.LogException(e);
                return StorageReadStatus.Error;
            }
        }

        public bool Save(string data) => WriteAtomic(MainPath, data);

        public bool Backup(string data, string stamp) =>
            WriteAtomic(Path.Combine(_folder, $"{FileName}_{stamp}{Extension}"), data);

        private bool WriteAtomic(string path, string data)
        {
            var temp = path + TempExtension;
            try
            {
                FileBus.CreateFolders(_folder);
                File.WriteAllText(temp, data ?? string.Empty);

                if (File.Exists(path))
                    File.Replace(temp, path, null);
                else
                    File.Move(temp, path);

                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[RebindFileDriver] Ошибка записи {path}");
                Debug.LogException(e);
                return false;
            }
        }
    }
}
