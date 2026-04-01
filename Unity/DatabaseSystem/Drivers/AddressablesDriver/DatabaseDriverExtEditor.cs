#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
#if ENABLE_ADDRESSABLES
using UnityEngine.AddressableAssets;
#endif
using Vortex.Core.DatabaseSystem;
using Vortex.Core.DatabaseSystem.Bus;
using Vortex.Core.SettingsSystem.Bus;
using Vortex.Unity.DatabaseSystem.Presets;
using Vortex.Unity.FileSystem.Bus;
using UnityEditor;
using Object = System.Object;

namespace Vortex.Unity.DatabaseSystem.Drivers.AddressablesDriver

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

            File.CreateFolders($"{Application.dataPath}/{Path}");
            Instance.ReloadDatabase();
        }

        public void ReloadDatabase()
        {
            if (Application.isPlaying)
                return;
            DatabaseDriverBase.Clean();
#if ENABLE_ADDRESSABLES
            var labels = Settings.Data().DatabaseLabels;
            if (labels == null || labels.Length == 0)
            {
                Debug.LogError(
                    "[DatabaseDriver] Метки (лейблы) не заданы в DatabaseSettings. Ассеты базы данных должны быть типа Addressable и помечены соответствующей меткой. Эти метки необходимо указать в DatabaseSettings.");
                return;
            }

            var ar = new List<IRecordPreset>();
            foreach (var label in labels)
            {
                var op = Addressables.LoadAssetsAsync<IRecordPreset>(label, null);
                var temp = op.WaitForCompletion();
                ar.AddRange(temp);
                Addressables.Release(op);
            }

            _resources = new UnityEngine.Object[ar.Count];
            Array.Copy(ar.ToArray(), _resources, ar.Count);
            foreach (var resource in _resources)
            {
                if (resource is not IRecordPreset data)
                    continue;
                DatabaseDriverBase.PutData(data);
            }
#endif
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