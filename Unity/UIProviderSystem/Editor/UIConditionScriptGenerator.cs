#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Vortex.Unity.UIProviderSystem.Editor
{
    /// <summary>
    /// Генератор скриптов-наследников UnityUserInterfaceCondition.
    /// Доступен через контекстное меню Project: Create > Vortex > UI Condition
    /// </summary>
    internal static class UIConditionScriptGenerator
    {
        private const string MenuPath = "Assets/Create/Vortex Templates/UI Condition";
        private const int MenuPriority = 80;

        private const string DefaultClassName = "NewUICondition";
        private const string FileExtension = ".cs";

        [MenuItem(MenuPath, priority = MenuPriority)]
        private static void CreateUIConditionScript()
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
        private static bool ValidateCreateUIConditionScript()
        {
            return Selection.activeObject == null || IsFolder(Selection.activeObject);
        }

        private static string GenerateScriptContent(string className)
        {
            return $@"using Vortex.Core.UIProviderSystem.Model;
using Vortex.Unity.UIProviderSystem.Model;

/// <summary>
/// TODO: Описание условия
/// </summary>
public sealed class {className} : UnityUserInterfaceCondition
{{
    protected override void Run()
    {{
        // TODO: Подписаться на события, которые могут изменить результат Check()
        // Пример: SomeEvent += RunCallback;
        RunCallback(); //Явный вызов метода запускающего проверку при инициализации 
    }}

    public override void DeInit()
    {{
        // TODO: Отписаться от событий
        // Пример: SomeEvent -= RunCallback;
    }}

    public override ConditionAnswer Check()
    {{
        // TODO: Реализовать логику проверки условия
        // Возвращаемые значения:
        //   ConditionAnswer.Open  - UI должен быть открыт
        //   ConditionAnswer.Close - UI должен быть закрыт
        //   ConditionAnswer.Idle  - условие не влияет на состояние
        return ConditionAnswer.Idle;
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