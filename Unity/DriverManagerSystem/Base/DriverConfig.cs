using System.Text;
using UnityEngine;
using Vortex.Core.System.Abstractions;
using Vortex.Unity.CoreAssetsSystem;
using Sirenix.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Sirenix.OdinInspector;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Vortex.Unity.DriverManagerSystem.Base
{
    /// <summary>
    /// Настройки используемых системами драйверов.
    /// Предназначен для однозначного указания какой именно драйвер будет использоваться системой
    /// (DI паттерн на минималках)
    /// </summary>
    public class DriverConfig : ScriptableObject, ICoreAsset
    {
        [SerializeReference, HideReferenceObjectPicker, ListDrawerSettings(HideAddButton = true, IsReadOnly = true)]
        private DriverRecord[] drivers = new DriverRecord[0];

        [InfoBox("Искать драйверы только в ru.vortex* пакетах")] [SerializeField]
        private bool onlyInVortexSearch = true;

        /// <summary>
        /// Возвращает назначенный указанной системе драйвер
        /// </summary>
        /// <param name="systemName"></param>
        /// <returns></returns>
        public string GetDriverForSystem(string systemName)
        {
            var rec = drivers.FirstOrDefault(r => r.SystemType == systemName);
            if (rec == null)
                return null;
            return rec.DriverType;
        }

#if UNITY_EDITOR

        private static bool _verified = false;

        [InitializeOnLoadMethod]
        private static void OnLoadRun()
        {
            if (_verified)
                return;
            _verified = true;
            var res = Resources.LoadAll<DriverConfig>("");
            if (res == null || res.Length == 0)
                return;
            var inst = res[0];
            inst.ReloadList();

            if (inst.drivers.Any(r => r.DriverType.IsNullOrWhitespace() || r.SystemType.IsNullOrWhitespace()))
                return;

            var text = inst.GetFileContent();
            var assetsPath = Application.dataPath;
            var allFiles = Directory.GetFiles(assetsPath, CfgFileName, SearchOption.AllDirectories);

            if (allFiles.Length <= 0)
            {
                Debug.Log($"[DriverConfig] Не найден файл DriversGenericList.cs.");
                return;
            }

            var targetPath = allFiles[0];
            var file = File.ReadAllText(targetPath);
            if (!file.Equals(text))
                Debug.LogWarning(
                    "[DriverConfig] \u26a0 Сохраненный файл конфигурации драйверов не соответствует таблице драйверов в ассете конфигурации");
        }

        [InfoBox("Сканирование системы и перезаполнение таблицы драйверов")]
        [Button("Reload")]
        private void ReloadList()
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            if (onlyInVortexSearch)
                assemblies = assemblies.Where(a => a.FullName.StartsWith("ru.vortex")).ToArray();

            var result = new List<DriverRecord>();
            var index = drivers?.Select(d => d.SystemType).Where(s => !s.IsNullOrWhitespace())
                .ToArray() ?? Array.Empty<string>();
            foreach (var assembly in assemblies)
            {
                var types = assembly.GetTypes().Where(t =>
                    !t.IsAbstract
                    && !t.IsInterface
                    && typeof(ISystemController).IsAssignableFrom(t));

                foreach (var system in types)
                {
                    var typeName = system.AssemblyQualifiedName;
                    if (drivers != null && index.Contains(typeName))
                    {
                        result.Add(drivers.FirstOrDefault(d => d.SystemType == typeName));
                        continue;
                    }

                    var rec = new DriverRecord(system.AssemblyQualifiedName);
                    result.Add(rec);
                }
            }

            drivers = result.ToArray();
        }

        private const string SystemPackageRoute = @"Vortex\Core\System";
        private const string CfgFileName = "DriversGenericList.cs";

        [InfoBox("Сохраняет текущий конфиг в файл DriversGenericList.cs в режиме кодогенерации")]
        [Button("Save Config")]
        private void SaveConfig()
        {
            if (drivers.Any(r => r.DriverType.IsNullOrWhitespace()
                                 || r.SystemType.IsNullOrWhitespace()
                                 && r.SystemType == " "))
            {
                Debug.LogError("[DriverConfig] Нельзя сохранять недозаполненный конфиг");
                return;
            }

            var assetsPath = Application.dataPath;
            var allFiles = Directory.GetFiles(assetsPath, CfgFileName, SearchOption.AllDirectories);

            if (allFiles.Length <= 0)
            {
                File.Create(Path.Combine(assetsPath, SystemPackageRoute, CfgFileName)).Close();
                allFiles = Directory.GetFiles(assetsPath, CfgFileName, SearchOption.AllDirectories);
            }

            var targetPath = allFiles[0];
            File.WriteAllText(targetPath, GetFileContent(), Encoding.UTF8);

            if (targetPath.StartsWith(assetsPath))
            {
                string assetPath = "Assets" + targetPath.Substring(assetsPath.Length).Replace("\\", "/");
                AssetDatabase.ImportAsset(assetPath);
            }

            Debug.Log($"[DriversListFileGenerator] Конфигурация записана");
        }

        /// <summary>
        /// Кодогенерация файла конфигурации
        /// </summary>
        /// <returns></returns>
        private string GetFileContent()
        {
            var sb = new StringBuilder();
            sb.AppendLine("//Автогенерированный конфиг файл. Не изменять в ручную!");
            sb.Append(
                "using System.Collections.Generic;\n\nnamespace Vortex.Core.System\n{\n    public static class DriversGenericList\n    {\n        public static Dictionary<string, string> WhiteList { get; } = new()\n        {");

            foreach (var driver in drivers)
                sb.Append(
                    $"\n            {{\n                \"{driver.SystemType}\",\n                \"{driver.DriverType}\"\n            }},");
            sb.Append("\n        };\n    }\n}");

            return sb.ToString();
        }
#endif
    }
}