using System;
using UnityEngine;
using Vortex.Core.Extensions.LogicExtensions;
using Vortex.Core.SettingsSystem;
using Vortex.Core.SettingsSystem.Model;
using Vortex.Core.System.Abstractions;
using Vortex.Unity.FileSystem.Bus;
using Vortex.Unity.SettingsSystem.Presets;

namespace Vortex.Unity.SettingsSystem
{
    public partial class SettingsDriver : Singleton<SettingsDriver>, IDriver
    {
        private const string Path = "Settings";

        public event Action OnInit;

        private SettingsModel _model;

        public void Init() => LoadData();

        public void Destroy()
        {
            //Ignore
        }

        private SettingsModel Model
        {
            get
            {
                if (_model != null)
                    return _model;
                _model = new SettingsModel();
                LoadData();

                return _model;
            }
        }

        /// <summary>
        /// Создание физической папки Resources/Settings нужно ТОЛЬКО в редакторе —
        /// чтобы дизайнеры могли класть туда SettingsPreset. В билде Resources запечён,
        /// его не создать и не нужно: <see cref="Resources.LoadAll{T}(string)"/> читает
        /// запечённое независимо от наличия физической папки.
        ///
        /// На Android <c>Application.dataPath</c> указывает на сам apk-файл
        /// (<c>/data/app/.../base.apk</c>), и попытка создать в нём подпапку валит
        /// <c>IOException ('base.apk' already exists)</c> — это рушило инициализацию
        /// настроек и весь бут (чёрный экран после прелоадера). В плеере метод — no-op.
        /// </summary>
        private void CheckPath()
        {
#if UNITY_EDITOR
            FileBus.CreateFolders($"{Application.dataPath}/Resources/{Path}");
#endif
        }

        private bool LoadData()
        {
            CheckPath();
            var dataSets = Resources.LoadAll<SettingsPreset>(Path);
            foreach (var data in dataSets)
            {
                var result = Model.CopyFrom(data);
                if (result)
                    continue;
                Debug.LogError($"[SettingsDriver] Failed to load settings data from {Path}");
                return false;
            }

            OnInit?.Invoke();
            return true;
        }

        public SettingsModel GetData() => Model;
    }
}