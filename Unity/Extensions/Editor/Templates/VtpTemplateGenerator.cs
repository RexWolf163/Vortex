#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace Vortex.Unity.Extensions.Editor.Templates
{
    /// <summary>
    /// AI-generation
    /// 
    /// Утилита для генерации .cs файлов из .vtp шаблонов.
    /// Шаблон — текстовый файл UTF-8 с плейсхолдерами вида {!Key!}.
    /// Файлы в шаблоне разделяются маркерами вида:
    ///   //---{относительный/путь/ИмяФайла.cs}---
    /// Также утилита умеет формировать .vtp шаблон из существующей папки
    /// путём рекурсивного сканирования .cs файлов.
    /// </summary>
    public static class VtpTemplateGenerator
    {
        // ──────────────────────────────────────────────────────────────────
        //  Константы и регулярки
        // ──────────────────────────────────────────────────────────────────

        /// <summary>
        /// Паттерн маркера-разделителя файлов: //---{путь/Файл.cs}---
        /// </summary>
        private static readonly Regex FileSeparatorRegex = new Regex(
            @"^//---\{(.+?)\}---\s*$",
            RegexOptions.Compiled
        );

        /// <summary>
        /// Паттерн плейсхолдера в шаблоне: {!KeyName!}
        /// </summary>
        private static readonly Regex PlaceholderRegex = new Regex(
            @"\{\!(\w+)\!\}",
            RegexOptions.Compiled
        );

        /// <summary>
        /// UTF-8 без BOM.
        /// </summary>
        private static readonly Encoding Utf8NoBom = new UTF8Encoding(false);

        // ──────────────────────────────────────────────────────────────────
        //  Генерация .cs файлов из .vtp шаблона
        // ──────────────────────────────────────────────────────────────────

        /// <summary>
        /// Генерация .cs файлов из .vtp шаблона.
        /// </summary>
        /// <param name="templatePath">
        /// Абсолютный или относительный (от корня проекта) путь к .vtp файлу шаблона.
        /// </param>
        /// <param name="outputFolder">
        /// Корневая папка вывода в Project окне Unity (например "Assets/Scripts/MiniGames").
        /// Относительные пути из маркеров будут создаваться внутри этой папки.
        /// </param>
        /// <param name="substitutions">
        /// Словарь подстановок: ключ — имя плейсхолдера (без {! !}), значение — подставляемый текст.
        /// Например: { "MiniGameName", "Puzzle" }
        /// </param>
        /// <returns>Список путей созданных файлов (относительно корня Unity-проекта).</returns>
        public static List<string> Generate(
            string templatePath,
            string outputFolder,
            Dictionary<string, string> substitutions)
        {
            if (substitutions == null)
                throw new ArgumentNullException(nameof(substitutions));

            var createdFiles = new List<string>();

            try
            {
                // --- Чтение шаблона ---
                string fullTemplatePath = ResolveProjectPath(templatePath);

                if (!File.Exists(fullTemplatePath))
                    throw new FileNotFoundException($"Шаблон не найден: {fullTemplatePath}");

                string templateContent = File.ReadAllText(fullTemplatePath, Utf8NoBom);

                // --- Подстановка значений ---
                string processedContent = ApplySubstitutions(templateContent, substitutions);

                // --- Подстановка в маркерах (имена файлов тоже могут содержать плейсхолдеры) ---
                // Уже обработано выше — маркеры являются частью общего текста.

                // --- Разбиение на отдельные файлы по маркерам ---
                Dictionary<string, string> fileBlocks = SplitByMarkers(processedContent);

                if (fileBlocks.Count == 0)
                {
                    Debug.LogWarning("[VtpTemplateGenerator] Шаблон не содержит маркеров файлов (//---{путь}---).");
                    return createdFiles;
                }

                // --- Создание файлов ---
                string fullOutputRoot = Path.GetFullPath(outputFolder);

                foreach (var kvp in fileBlocks)
                {
                    string relativePath = kvp.Key; // например "Services/PuzzleService.cs"
                    string content = kvp.Value;

                    try
                    {
                        string fullFilePath = Path.Combine(fullOutputRoot, relativePath);
                        string fileDir = Path.GetDirectoryName(fullFilePath);

                        if (!string.IsNullOrEmpty(fileDir) && !Directory.Exists(fileDir))
                            Directory.CreateDirectory(fileDir);

                        File.WriteAllText(fullFilePath, content.TrimEnd() + Environment.NewLine, Utf8NoBom);

                        string unityRelativePath = Path.Combine(outputFolder, relativePath);
                        createdFiles.Add(unityRelativePath);

                        Debug.Log($"[VtpTemplateGenerator] Создан файл: {unityRelativePath}");
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[VtpTemplateGenerator] Ошибка записи файла '{relativePath}': {ex.Message}");
                    }
                }

                AssetDatabase.Refresh();
                Debug.Log($"[VtpTemplateGenerator] Готово. Создано файлов: {createdFiles.Count}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[VtpTemplateGenerator] Критическая ошибка генерации: {ex.Message}\n{ex.StackTrace}");
            }

            return createdFiles;
        }

        // ──────────────────────────────────────────────────────────────────
        //  Формирование .vtp шаблона из папки
        // ──────────────────────────────────────────────────────────────────

        /// <summary>
        /// Рекурсивно сканирует указанную папку, собирает все .cs файлы
        /// и формирует из них .vtp шаблон с маркерами-разделителями.
        /// Пути в маркерах сохраняются относительно <paramref name="sourceFolder"/>.
        /// </summary>
        /// <param name="sourceFolder">
        /// Путь к исходной папке (например "Assets/Scripts/MiniGames/Puzzle").
        /// </param>
        /// <param name="outputTemplatePath">
        /// Путь для сохранения .vtp файла (например "Assets/Templates/Puzzle.vtp").
        /// </param>
        /// <param name="replacements">
        /// Опциональный словарь обратных подстановок: значение → плейсхолдер.
        /// Например: { "Puzzle", "MiniGameName" } заменит все вхождения "Puzzle" на {!MiniGameName!}
        /// как в содержимом файлов, так и в путях маркеров.
        /// Если null — файлы копируются как есть.
        /// </param>
        /// <returns>Полный путь к созданному .vtp файлу, или null при ошибке.</returns>
        public static string CreateTemplateFromFolder(
            string sourceFolder,
            string outputTemplatePath,
            Dictionary<string, string> replacements = null)
        {
            try
            {
                string fullSourceFolder = ResolveProjectPath(sourceFolder);

                if (!Directory.Exists(fullSourceFolder))
                    throw new DirectoryNotFoundException($"Папка не найдена: {fullSourceFolder}");

                // Собираем все .cs файлы рекурсивно, сортируем для стабильного порядка
                string[] csFiles = Directory.GetFiles(fullSourceFolder, "*.cs", SearchOption.AllDirectories);
                Array.Sort(csFiles, StringComparer.OrdinalIgnoreCase);

                if (csFiles.Length == 0)
                {
                    Debug.LogWarning($"[VtpTemplateGenerator] В папке '{sourceFolder}' не найдено .cs файлов.");
                    return null;
                }

                var sb = new StringBuilder();

                for (int i = 0; i < csFiles.Length; i++)
                {
                    string fullPath = csFiles[i];
                    string relativePath = GetRelativePath(fullSourceFolder, fullPath);

                    // Нормализуем разделители в прямые слеши для единообразия
                    relativePath = relativePath.Replace('\\', '/');

                    string fileContent;
                    try
                    {
                        fileContent = File.ReadAllText(fullPath, Utf8NoBom);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError(
                            $"[VtpTemplateGenerator] Не удалось прочитать файл '{fullPath}': {ex.Message}");
                        continue;
                    }

                    // Обратная подстановка: заменяем конкретные значения на плейсхолдеры
                    if (replacements != null && replacements.Count > 0)
                    {
                        relativePath = ApplyReverseSubstitutions(relativePath, replacements);
                        fileContent = ApplyReverseSubstitutions(fileContent, replacements);
                    }

                    // Маркер-разделитель
                    if (i > 0)
                        sb.AppendLine(); // пустая строка между блоками

                    sb.AppendLine($"//---{{{relativePath}}}---");
                    sb.Append(fileContent.TrimEnd());
                    sb.AppendLine();
                }

                // Записываем .vtp файл
                string fullOutputPath = ResolveProjectPath(outputTemplatePath);
                string outputDir = Path.GetDirectoryName(fullOutputPath);

                if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
                    Directory.CreateDirectory(outputDir);

                File.WriteAllText(fullOutputPath, sb.ToString(), Utf8NoBom);

                AssetDatabase.Refresh();
                Debug.Log(
                    $"[VtpTemplateGenerator] Шаблон создан: {outputTemplatePath} ({csFiles.Length} файлов)");

                return fullOutputPath;
            }
            catch (Exception ex)
            {
                Debug.LogError(
                    $"[VtpTemplateGenerator] Ошибка создания шаблона: {ex.Message}\n{ex.StackTrace}");
                return null;
            }
        }

        // ──────────────────────────────────────────────────────────────────
        //  Вспомогательные методы
        // ──────────────────────────────────────────────────────────────────

        /// <summary>
        /// Подставляет значения из словаря вместо плейсхолдеров {!Key!}.
        /// </summary>
        private static string ApplySubstitutions(
            string content, Dictionary<string, string> substitutions)
        {
            return PlaceholderRegex.Replace(content, match =>
            {
                string key = match.Groups[1].Value;

                if (substitutions.TryGetValue(key, out string value))
                    return value;

                Debug.LogWarning(
                    $"[VtpTemplateGenerator] Плейсхолдер '{{!{key}!}}' не найден в словаре подстановок.");
                return match.Value;
            });
        }

        /// <summary>
        /// Обратная подстановка: заменяет конкретные значения на плейсхолдеры {!Key!}.
        /// Замены выполняются от длинных значений к коротким, чтобы избежать частичных замен.
        /// </summary>
        private static string ApplyReverseSubstitutions(
            string content, Dictionary<string, string> replacements)
        {
            // Сортируем по убыванию длины ключа (значения, которое ищем),
            // чтобы "PuzzleGame" заменился раньше, чем "Puzzle"
            var sorted = replacements
                .OrderByDescending(kvp => kvp.Key.Length)
                .ToList();

            foreach (var kvp in sorted)
            {
                string searchValue = kvp.Key;
                string placeholderName = kvp.Value;

                if (string.IsNullOrEmpty(searchValue))
                    continue;

                content = content.Replace(searchValue, $"{{!{placeholderName}!}}");
            }

            return content;
        }

        /// <summary>
        /// Разбивает содержимое шаблона на блоки по маркерам //---{путь}---.
        /// Возвращает словарь: относительный путь → содержимое файла.
        /// </summary>
        private static Dictionary<string, string> SplitByMarkers(string content)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var lines = content.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);

            string currentPath = null;
            var currentLines = new List<string>();

            foreach (string line in lines)
            {
                Match markerMatch = FileSeparatorRegex.Match(line.Trim());

                if (markerMatch.Success)
                {
                    // Сохраняем предыдущий блок, если есть
                    if (currentPath != null)
                    {
                        result[currentPath] = JoinBlock(currentLines);
                        currentLines.Clear();
                    }

                    currentPath = markerMatch.Groups[1].Value.Trim();
                }
                else
                {
                    // Строки до первого маркера игнорируются (или можно логировать)
                    if (currentPath != null)
                        currentLines.Add(line);
                }
            }

            // Последний блок
            if (currentPath != null)
                result[currentPath] = JoinBlock(currentLines);

            return result;
        }

        /// <summary>
        /// Склеивает строки блока, убирая лишние пустые строки в начале и конце.
        /// </summary>
        private static string JoinBlock(List<string> lines)
        {
            if (lines == null || lines.Count == 0)
                return string.Empty;

            var sb = new StringBuilder();
            for (int i = 0; i < lines.Count; i++)
            {
                sb.Append(lines[i]);
                if (i < lines.Count - 1)
                    sb.AppendLine();
            }

            return sb.ToString().Trim();
        }

        /// <summary>
        /// Резолвит путь. Если не абсолютный — считает относительным от корня Unity-проекта.
        /// </summary>
        private static string ResolveProjectPath(string path)
        {
            if (Path.IsPathRooted(path))
                return path;

            // Application.dataPath = ".../ProjectRoot/Assets"
            // Корень проекта — на уровень выше
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            return Path.GetFullPath(Path.Combine(projectRoot, path));
        }

        /// <summary>
        /// Возвращает относительный путь файла от базовой папки.
        /// </summary>
        private static string GetRelativePath(string basePath, string fullPath)
        {
            // Нормализуем оба пути
            basePath = Path.GetFullPath(basePath);
            fullPath = Path.GetFullPath(fullPath);

            if (!basePath.EndsWith(Path.DirectorySeparatorChar.ToString()))
                basePath += Path.DirectorySeparatorChar;

            if (fullPath.StartsWith(basePath, StringComparison.OrdinalIgnoreCase))
                return fullPath.Substring(basePath.Length);

            // Fallback — просто имя файла
            return Path.GetFileName(fullPath);
        }
    }
}
#endif