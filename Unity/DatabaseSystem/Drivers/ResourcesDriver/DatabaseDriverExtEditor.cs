#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEngine;
using Vortex.Core.DatabaseSystem;
using Vortex.Core.DatabaseSystem.Bus;
using Vortex.Core.SettingsSystem.Bus;
using Vortex.Unity.DatabaseSystem.Presets;
using Vortex.Unity.FileSystem.Bus;
using Object = System.Object;

namespace Vortex.Unity.DatabaseSystem.Drivers.ResourcesDriver
{
    public partial class DatabaseDriver : IDriverEditor
    {
        [InitializeOnLoadMethod]
        private static void EditorRegister()
        {
            if (Application.isPlaying)
                return;
            if (!Database.SetDriver(Instance))
                return;

            File.CreateFolders($"{Application.dataPath}/Resources/{Path}");
            Instance.ReloadDatabase();
        }

        public void ReloadDatabase()
        {
            if (Application.isPlaying)
                return;
            DatabaseDriverBase.Clean();

            if (Settings.Data() == null)
                return;

            _resources = Resources.LoadAll(Path);
            foreach (var resource in _resources)
            {
                if (resource is not IRecordPreset data)
                    continue;
                DatabaseDriverBase.PutData(data);
            }
        }

        /// <summary>
        /// Возвращает пресет по GUID
        /// Внимание! Использовать только для настройки!
        /// </summary>
        /// <param name="guid"></param>
        /// <returns></returns>
        public Object GetPresetForRecord(string guid)
        {
            if (_resources == null)
                Instance.ReloadDatabase();
            if (_resources != null)
                return _resources.FirstOrDefault(x => (x as IRecordPreset)?.GuidPreset == guid);
            Debug.LogError("[DatabaseDriver] Ошибка при загрузке ресурсов Базы данных");
            return null;
        }
    }
}
#endif