#if UNITY_EDITOR
using System.IO;
using System.Linq;
using System.Text;
using Cysharp.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using System;

namespace Vortex.Unity.Extensions.Editor.AITools
{
    /// <summary>
    /// Инструмент для формирования файлов контекста для выгрузки их во внешнего ИИ ассистента
    /// </summary>
    public static class AIContextCreator
    {
        private static readonly string DividerLine = "// " + new string('=', 60);

        [MenuItem("Assets/Vortex/Debug/Create Context for AI")]
        private static async void CombineCSharpFilesInFolder()
        {
            await CombineFilesInFolder("*.cs", "_");
            await CombineFilesInFolder("*.md", "_MD_");
        }

        private static async UniTask CombineFilesInFolder(string searchPattern, string suffix)
        {
            var selectedObject = Selection.activeObject;
            if (selectedObject == null)
            {
                Debug.LogError("No folder selected.");
                return;
            }

            var folderPath = AssetDatabase.GetAssetPath(selectedObject);
            if (!Directory.Exists(folderPath))
            {
                Debug.LogError($"Selected item is not a valid folder: {folderPath}");
                return;
            }

            var files = Directory.GetFiles(folderPath, searchPattern, SearchOption.AllDirectories);

            if (files.Length == 0)
            {
                Debug.LogWarning($"No {searchPattern} files found in the selected folder or its subfolders.");
                return;
            }

            var combinedContent = new StringBuilder();
            var processedCount = 0;

            foreach (string filePath in files.OrderBy(f => f))
            {
                try
                {
                    string content = File.ReadAllText(filePath, Encoding.UTF8);
                    string relativePath = filePath.Substring("Assets".Length + 1);

                    combinedContent.AppendLine(DividerLine);
                    combinedContent.AppendLine($"// FILE: {relativePath}");
                    combinedContent.AppendLine(DividerLine);
                    combinedContent.AppendLine(content);
                    combinedContent.AppendLine(); // пустая строка между файлами
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"Failed to process file: {filePath}\n{ex.Message}");
                }

                processedCount++;
                if (processedCount % 100 == 0)
                    await UniTask.Yield();
            }

            // Сохраняем результат вне Assets (на уровень выше проекта)
            var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
            var outputFileName = $"{Path.GetFileName(folderPath)}{suffix}{DateTime.UtcNow.ToFileTimeUtc()}.txt";
            var outputPath = Path.Combine(projectRoot, outputFileName);

            File.WriteAllText(outputPath, combinedContent.ToString(), Encoding.UTF8);

            Debug.Log($"✅ Combined {files.Length} {searchPattern} files into:\n{outputPath}");
        }

        /// <summary>
        /// Проверка доступности пункта меню: только для папок
        /// </summary>
        /// <returns></returns>
        [MenuItem("Assets/Vortex/Debug/Create Context for AI", validate = true)]
        private static bool ValidateCombineCSharpFilesInFolder()
        {
            if (Selection.objects.Length != 1)
                return false;

            var obj = Selection.activeObject;
            if (obj == null)
                return false;

            var path = AssetDatabase.GetAssetPath(obj);
            return !string.IsNullOrEmpty(path) && Directory.Exists(path);
        }
    }
}
#endif