#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Vortex.Unity.Extensions.Editor;
using Vortex.Unity.Extensions.Editor.Templates;

namespace Vortex.Sdk.MiniGamesSystem.MiniGames.Editor
{
    /// <summary>
    /// Регистрирует пункт контекстного меню в окне Project для генерации
    /// файлов миниигры из .vtp шаблона.
    /// Шаблон MiniGame.vtp должен лежать рядом с этим скриптом.
    /// </summary>
    public static class MiniGameTemplateMenu
    {
        /// <summary>
        /// Имя файла шаблона, расположенного рядом с этим скриптом.
        /// </summary>
        private const string TemplateName = "minigames.vtp";

        /// <summary>
        /// Хардкодные параметры подстановки.
        /// </summary>
        private static readonly Dictionary<string, string> Substitutions = new()
        {
        };

        [MenuItem("Assets/Create/Vortex Templates/MiniGame", false, 80)]
        private static void Generate()
        {
            string outputFolder = GetSelectedFolderPath();

            if (string.IsNullOrEmpty(outputFolder))
            {
                EditorUtility.DisplayDialog(
                    "Ошибка",
                    "Не удалось определить папку. Выберите папку в окне Project.",
                    "OK"
                );
                return;
            }

            string templatePath = GetTemplatePath();

            if (string.IsNullOrEmpty(templatePath) || !File.Exists(templatePath))
            {
                EditorUtility.DisplayDialog(
                    "Ошибка",
                    $"Шаблон '{TemplateName}' не найден рядом с {nameof(MiniGameTemplateMenu)}.cs",
                    "OK"
                );
                return;
            }

            //Открывает UI для ввода названия миниигры
            VtpGeneratorWindow.ShowWindow(templatePath, outputFolder, Substitutions);
        }

        [MenuItem("Assets/Create/Vortex Templates/MiniGame", true)]
        private static bool GenerateValidation()
        {
            // Пункт меню активен только если выбрана папка
            return !string.IsNullOrEmpty(GetSelectedFolderPath());
        }

        /// <summary>
        /// Возвращает путь к папке, выбранной в окне Project.
        /// Если выбран файл — возвращает его родительскую папку.
        /// </summary>
        private static string GetSelectedFolderPath()
        {
            foreach (Object obj in Selection.GetFiltered<Object>(SelectionMode.Assets))
            {
                string path = AssetDatabase.GetAssetPath(obj);

                if (string.IsNullOrEmpty(path))
                    continue;

                if (AssetDatabase.IsValidFolder(path))
                    return path;

                // Выбран файл — берём папку
                string directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory))
                    return directory;
            }

            return null;
        }

        /// <summary>
        /// Определяет путь к .vtp шаблону, лежащему рядом с этим скриптом.
        /// Использует поиск по AssetDatabase, чтобы не зависеть от захардкоженного пути.
        /// </summary>
        private static string GetTemplatePath()
        {
            // Находим этот скрипт через AssetDatabase
            string[] guids = AssetDatabase.FindAssets($"t:Script {nameof(MiniGameTemplateMenu)}");

            foreach (string guid in guids)
            {
                string scriptPath = AssetDatabase.GUIDToAssetPath(guid);

                // Проверяем что это именно наш файл
                if (!scriptPath.EndsWith($"{nameof(MiniGameTemplateMenu)}.cs"))
                    continue;

                string scriptFolder = Path.GetDirectoryName(scriptPath);
                string templatePath = Path.Combine(scriptFolder, TemplateName);

                return templatePath;
            }

            return null;
        }
    }
}
#endif