using System;
using System.IO;
using System.Linq;
using UnityEngine;
using Vortex.Core.LoaderSystem.Bus;
using Vortex.Core.SaveSystem;
using Vortex.Core.SaveSystem.Bus;
using Vortex.Core.SettingsSystem.Bus;
using Vortex.Core.System.Abstractions;
using Vortex.Unity.AppSystem.System.TimeSystem;
using Vortex.Unity.FileSystem.Bus;

namespace Vortex.Unity.SaveSystem.Drivers.GlobalFileDriver
{
    /// <summary>
    /// Драйвер глобального хранилища в файле. Папка — из <c>SaveSettings</c> относительно корня данных
    /// приложения (<see cref="FileBus.GetAppPath"/>), той же базы, что у <c>FileSystemDriver</c>; имя фиксированное,
    /// без расширения <c>.summary</c> — в список сейвов файл не попадает. Копии — соседние файлы
    /// <c>GlobalSave_copy_{id}.dat</c>.
    ///
    /// Запись атомарная: сначала во временный файл, затем подмена основного. Оборванная запись оставляет прежний
    /// контейнер целым.
    /// </summary>
    public sealed class GlobalFileDriver : Singleton<GlobalFileDriver>, IGlobalSaveDriver
    {
        private const string FileName = "GlobalSave";
        private const string CopyInfix = "_copy_";
        private const string Extension = ".dat";
        private const string TempExtension = ".tmp";

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

        /// <summary>
        /// Папка вычисляется при обращении, а не в <see cref="Init"/>: порядок <c>[RuntimeInitializeOnLoadMethod]</c>
        /// не гарантирован, и в билде драйвер подключается раньше настроек. Чтение идёт из очереди загрузки —
        /// настройки к этому моменту загружены; без них — исключение, а не тихий откат в корень.
        /// </summary>
        private static string Folder =>
            Path.Combine(FileBus.GetAppPath(), Settings.Data().GlobalSaveFolder ?? string.Empty);

        private static string MainPath => Path.Combine(Folder, FileName + Extension);

        private static string CopyPath(string id) => Path.Combine(Folder, $"{FileName}{CopyInfix}{id}{Extension}");

        public GlobalReadStatus Read(out string data)
        {
            data = null;
            var path = MainPath;
            if (!File.Exists(path))
                return GlobalReadStatus.NoData;

            try
            {
                data = File.ReadAllText(path);
                return GlobalReadStatus.Ok;
            }
            catch (Exception e)
            {
                Debug.LogError($"[GlobalFileDriver] Ошибка чтения {path}");
                Debug.LogException(e);
                return GlobalReadStatus.Error;
            }
        }

        public bool Write(string data) => WriteAtomic(MainPath, data);

        public bool WriteCopy(string id, string data) => WriteAtomic(CopyPath(id), data);

        public string[] GetCopies()
        {
            try
            {
                if (!Directory.Exists(Folder))
                    return Array.Empty<string>();

                var prefix = FileName + CopyInfix;
                return Directory.GetFiles(Folder, $"{prefix}*{Extension}")
                    .Select(file => Path.GetFileNameWithoutExtension(file))
                    .Where(name => name.Length > prefix.Length)
                    .Select(name => name.Substring(prefix.Length))
                    .ToArray();
            }
            catch (Exception e)
            {
                Debug.LogError("[GlobalFileDriver] Ошибка перечисления копий");
                Debug.LogException(e);
                return Array.Empty<string>();
            }
        }

        public bool ReadCopy(string id, out string data)
        {
            data = null;
            var path = CopyPath(id);
            if (!File.Exists(path))
                return false;

            try
            {
                data = File.ReadAllText(path);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[GlobalFileDriver] Ошибка чтения {path}");
                Debug.LogException(e);
                return false;
            }
        }

        public void DeleteCopy(string id)
        {
            var path = CopyPath(id);
            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch (Exception e)
            {
                Debug.LogError($"[GlobalFileDriver] Ошибка удаления {path}");
                Debug.LogException(e);
            }
        }

        public void ScheduleFlush(Action flush) => TimeController.Call(flush, this);

        private static bool WriteAtomic(string path, string data)
        {
            var temp = path + TempExtension;
            try
            {
                FileBus.CreateFolders(Folder);
                File.WriteAllText(temp, data ?? string.Empty);

                if (File.Exists(path))
                    File.Replace(temp, path, null);
                else
                    File.Move(temp, path);

                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[GlobalFileDriver] Ошибка записи {path}");
                Debug.LogException(e);
                return false;
            }
        }
    }
}
