#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Vortex.Unity.DatabaseSystemEditor.Editor
{
    /// <summary>
    /// Генератор скриптов-наследников Record.
    /// Доступен через контекстное меню Project: Create > Vortex > Record
    ///
    /// AI-код
    /// </summary>
    internal static class RecordScriptGenerator
    {
        private const string MenuPath = "Assets/Create/Vortex Templates/Record";
        private const int MenuPriority = 81;

        private const string DefaultClassName = "NewRecord";
        private const string FileExtension = ".cs";

        [MenuItem(MenuPath, priority = MenuPriority)]
        private static void CreateRecordScript()
        {
            var targetFolder = GetSelectedFolderPath();
            var fullPath = GenerateUniqueFilePath(targetFolder, DefaultClassName);
            var className = Path.GetFileNameWithoutExtension(fullPath);

            var content = GenerateScriptContent(className);

            File.WriteAllText(fullPath, content);
            AssetDatabase.Refresh();

            var asset = AssetDatabase.LoadAssetAtPath<MonoScript>(ToAssetPath(fullPath));
            if (asset == null)
                return;
            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
        }

        [MenuItem(MenuPath, validate = true)]
        private static bool ValidateCreateRecordScript()
        {
            return Selection.activeObject == null || IsFolder(Selection.activeObject);
        }

        private static string GenerateScriptContent(string className)
        {
            return $@"using Vortex.Core.DatabaseSystem.Model;
using Vortex.Core.Extensions.LogicExtensions.SerializationSystem;
using Vortex.Core.System.Abstractions.SystemControllers;

/// <summary>
/// TODO: Описание записи
/// </summary>
public class {className} : Record
{{
    // TODO: Добавить свойства с {{ get; protected set; }}
    // Свойства копируются из Preset через CopyFrom автоматически

    public override string GetDataForSave() => this.SerializeProperties();

    public override void LoadFromSaveData(string data)
    {{
        var temp = data.DeserializeProperties<{className}>();
        this.CopyFrom(temp);
    }}
}}
";
        }

        private static string GetSelectedFolderPath()
        {
            var selected = Selection.activeObject;
            if (selected == null)
                return Application.dataPath;

            var path = AssetDatabase.GetAssetPath(selected);

            if (Directory.Exists(path))
                return path;

            return Path.GetDirectoryName(path) ?? Application.dataPath;
        }

        private static string GenerateUniqueFilePath(string folder, string baseName)
        {
            var candidate = Path.Combine(folder, baseName + FileExtension);

            if (!File.Exists(candidate))
                return candidate;

            var counter = 1;
            while (File.Exists(candidate))
            {
                candidate = Path.Combine(folder, $"{baseName}{counter}{FileExtension}");
                counter++;
            }

            return candidate;
        }

        private static bool IsFolder(Object obj)
        {
            if (obj == null)
                return false;

            var path = AssetDatabase.GetAssetPath(obj);
            return !string.IsNullOrEmpty(path) && Directory.Exists(path);
        }

        private static string ToAssetPath(string fullPath)
        {
            var normalized = fullPath.Replace('\\', '/');
            var dataPath = Application.dataPath.Replace('\\', '/');

            if (normalized.StartsWith(dataPath))
                return "Assets" + normalized.Substring(dataPath.Length);

            return normalized;
        }
    }
}
#endif