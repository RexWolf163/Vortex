#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Vortex.Unity.Extensions.Editor.Templates
{
    /// <summary>
    /// AI-generation
    /// 
    /// Контекстное меню и окно-визард для создания .vtp шаблона из папки.
    /// ПКМ по папке в Project → "Assets/Vortex/Template Generator" — открывает окно,
    /// в котором можно указать имя шаблона, добавить обратные подстановки
    /// и запустить генерацию .vtp файла.
    /// 
    /// Результат (сохраняется в исходную папку):
    ///   1. {ИмяПапки}.vtp — шаблон.
    ///   2. {ИмяПапки}TemplateMenu.cs — скрипт контекстного меню для генерации из шаблона.
    /// </summary>
    public class VtpTemplateCreatorWindow : EditorWindow
    {
        // ──────────────────────────────────────────────────────────────────
        //  Константы
        // ──────────────────────────────────────────────────────────────────

        private static readonly Vector2 WindowSize = new Vector2(500, 380);

        /// <summary>
        /// Имя .vtp шаблона для генерации TemplateMenu скриптов.
        /// Файл должен лежать рядом с этим скриптом (VtpTemplateCreatorWindow.cs).
        /// </summary>
        private const string MenuTemplateFileName = "TemplateMenu.vtp";

        // ──────────────────────────────────────────────────────────────────
        //  Состояние окна
        // ──────────────────────────────────────────────────────────────────

        private string _sourceFolder;
        private string _templateName = "NewTemplate";

        private List<ReplacementEntry> _replacements = new List<ReplacementEntry>();
        private Vector2 _scrollPos;

        [System.Serializable]
        private class ReplacementEntry
        {
            public string SearchValue = "";
            public string PlaceholderName = "";
        }

        // ──────────────────────────────────────────────────────────────────
        //  Контекстное меню
        // ──────────────────────────────────────────────────────────────────

        /// <summary>
        /// Пункт контекстного меню: ПКМ по папке → Vortex → Template Generator.
        /// </summary>
        [MenuItem("Assets/Vortex/Template Generator", false, 90)]
        private static void CreateTemplateFromFolder()
        {
            string folderPath = GetSelectedFolderPath();

            if (string.IsNullOrEmpty(folderPath))
            {
                EditorUtility.DisplayDialog(
                    "VTP Template",
                    "Выберите папку в Project окне.",
                    "OK");
                return;
            }

            var window = GetWindow<VtpTemplateCreatorWindow>(
                utility: true,
                title: "Create VTP Template",
                focus: true);

            window.minSize = WindowSize;
            window.maxSize = WindowSize;
            window._sourceFolder = folderPath;

            // Предлагаем имя шаблона по имени папки
            window._templateName = Path.GetFileName(folderPath);

            // Предзаполняем первую подстановку именем папки
            window._replacements.Clear();
            window._replacements.Add(new ReplacementEntry
            {
                SearchValue = Path.GetFileName(folderPath),
                PlaceholderName = "ClassName"
            });

            window.Show();
        }

        /// <summary>
        /// Валидация: пункт меню активен только если выбрана папка.
        /// </summary>
        [MenuItem("Assets/Vortex/Template Generator", true)]
        private static bool CreateTemplateFromFolderValidation()
        {
            return !string.IsNullOrEmpty(GetSelectedFolderPath());
        }

        // ──────────────────────────────────────────────────────────────────
        //  GUI
        // ──────────────────────────────────────────────────────────────────

        private void OnGUI()
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Создание .vtp шаблона из папки", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            // --- Исходная папка ---
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PrefixLabel("Исходная папка");
                EditorGUI.BeginDisabledGroup(true);
                EditorGUILayout.TextField(_sourceFolder);
                EditorGUI.EndDisabledGroup();
            }

            // Подсчёт .cs файлов
            int csCount = CountCsFiles(_sourceFolder);
            EditorGUILayout.HelpBox(
                $"Найдено .cs файлов: {csCount}",
                csCount > 0 ? MessageType.Info : MessageType.Warning);

            EditorGUILayout.Space(4);

            // --- Имя шаблона ---
            _templateName = EditorGUILayout.TextField("Имя шаблона", _templateName);

            EditorGUILayout.Space(4);

            // --- Превью путей (read-only) ---
            string safeName = SanitizeFileName(_templateName);
            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.TextField("Шаблон", $"{_sourceFolder}/{safeName}.vtp");
            EditorGUILayout.TextField("Меню-скрипт", $"{_sourceFolder}/{safeName}TemplateMenu.cs");
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.Space(8);

            // --- Обратные подстановки ---
            EditorGUILayout.LabelField("Обратные подстановки", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Все вхождения «Искать» будут заменены на {!Плейсхолдер!} в шаблоне.\n" +
                "Например: Puzzle → {!ClassName!}",
                MessageType.None);

            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            for (int i = 0; i < _replacements.Count; i++)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    _replacements[i].SearchValue =
                        EditorGUILayout.TextField("Искать", _replacements[i].SearchValue);

                    _replacements[i].PlaceholderName =
                        EditorGUILayout.TextField("Плейсхолдер", _replacements[i].PlaceholderName);

                    if (GUILayout.Button("✕", GUILayout.Width(24)))
                    {
                        _replacements.RemoveAt(i);
                        GUIUtility.ExitGUI();
                    }
                }
            }

            EditorGUILayout.EndScrollView();

            if (GUILayout.Button("+ Добавить подстановку"))
                _replacements.Add(new ReplacementEntry());

            EditorGUILayout.Space(12);

            // --- Кнопка генерации ---
            EditorGUI.BeginDisabledGroup(
                string.IsNullOrWhiteSpace(_templateName) || csCount == 0);

            if (GUILayout.Button("Сгенерировать .vtp шаблон", GUILayout.Height(32)))
                DoGenerate();

            EditorGUI.EndDisabledGroup();
        }

        // ──────────────────────────────────────────────────────────────────
        //  Логика
        // ──────────────────────────────────────────────────────────────────

        private void DoGenerate()
        {
            // Собираем словарь обратных подстановок
            var replacements = new Dictionary<string, string>();

            foreach (var entry in _replacements)
            {
                if (string.IsNullOrWhiteSpace(entry.SearchValue) ||
                    string.IsNullOrWhiteSpace(entry.PlaceholderName))
                    continue;

                string key = entry.SearchValue.Trim();
                string value = entry.PlaceholderName.Trim();

                if (!replacements.ContainsKey(key))
                    replacements[key] = value;
            }

            string sanitizedName = SanitizeFileName(_templateName);

            // Оба файла сохраняются в исходную папку
            string vtpPath = Path.Combine(_sourceFolder, $"{sanitizedName}.vtp");
            string fullVtpPath = ResolveProjectPath(vtpPath);

            if (File.Exists(fullVtpPath))
            {
                bool overwrite = EditorUtility.DisplayDialog(
                    "Файл уже существует",
                    $"Шаблон '{vtpPath}' уже существует.\nПерезаписать?",
                    "Перезаписать",
                    "Отмена");

                if (!overwrite)
                    return;
            }

            // 1. Генерация .vtp
            string result = VtpTemplateGenerator.CreateTemplateFromFolder(
                _sourceFolder,
                vtpPath,
                replacements.Count > 0 ? replacements : null);

            if (string.IsNullOrEmpty(result))
            {
                EditorUtility.DisplayDialog(
                    "Ошибка",
                    "Не удалось создать шаблон. Проверьте Console.",
                    "OK");
                return;
            }

            // 2. Генерация TemplateMenu.cs рядом (через TemplateMenu.vtp)
            GenerateTemplateMenuScript(sanitizedName, _sourceFolder);
            string menuScriptPath = Path.Combine(_sourceFolder, $"{sanitizedName}TemplateMenu.cs");

            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog(
                "Готово",
                $"Созданы файлы:\n• {vtpPath}\n• {menuScriptPath}",
                "OK");

            // Подсвечиваем .vtp в Project окне
            var asset = AssetDatabase.LoadAssetAtPath<Object>(vtpPath);
            if (asset != null)
            {
                EditorGUIUtility.PingObject(asset);
                Selection.activeObject = asset;
            }

            Close();
        }

        /// <summary>
        /// Генерирует скрипт контекстного меню из TemplateMenu.vtp шаблона.
        /// </summary>
        private static void GenerateTemplateMenuScript(string templateName, string outputFolder)
        {
            string menuTemplatePath = FindMenuTemplatePath();

            if (string.IsNullOrEmpty(menuTemplatePath))
            {
                Debug.LogError(
                    $"[VtpTemplateCreatorWindow] Шаблон '{MenuTemplateFileName}' не найден " +
                    $"рядом с {nameof(VtpTemplateCreatorWindow)}.cs");
                return;
            }

            var substitutions = new Dictionary<string, string>
            {
                { "ClassName", templateName }
            };

            VtpTemplateGenerator.Generate(menuTemplatePath, outputFolder, substitutions);
        }

        /// <summary>
        /// Находит путь к TemplateMenu.vtp, лежащему рядом с этим скриптом.
        /// </summary>
        private static string FindMenuTemplatePath()
        {
            string[] guids = AssetDatabase.FindAssets(
                $"t:Script {nameof(VtpTemplateCreatorWindow)}");

            foreach (string guid in guids)
            {
                string scriptPath = AssetDatabase.GUIDToAssetPath(guid);

                if (!scriptPath.EndsWith($"{nameof(VtpTemplateCreatorWindow)}.cs"))
                    continue;

                string scriptFolder = Path.GetDirectoryName(scriptPath);
                string templatePath = Path.Combine(scriptFolder, MenuTemplateFileName);

                if (File.Exists(templatePath))
                    return templatePath;
            }

            return null;
        }

        // ──────────────────────────────────────────────────────────────────
        //  Хелперы
        // ──────────────────────────────────────────────────────────────────

        /// <summary>
        /// Возвращает путь выбранной папки в Project окне, или null.
        /// </summary>
        private static string GetSelectedFolderPath()
        {
            foreach (var obj in Selection.GetFiltered<Object>(SelectionMode.Assets))
            {
                string path = AssetDatabase.GetAssetPath(obj);

                if (string.IsNullOrEmpty(path))
                    continue;

                if (AssetDatabase.IsValidFolder(path))
                    return path;

                if (File.Exists(path))
                    return Path.GetDirectoryName(path);
            }

            return null;
        }

        /// <summary>
        /// Считает количество .cs файлов в папке рекурсивно.
        /// </summary>
        private static int CountCsFiles(string folderPath)
        {
            if (string.IsNullOrEmpty(folderPath))
                return 0;

            string fullPath = ResolveProjectPath(folderPath);

            if (!Directory.Exists(fullPath))
                return 0;

            return Directory.GetFiles(fullPath, "*.cs", SearchOption.AllDirectories).Length;
        }

        /// <summary>
        /// Резолвит путь относительно корня Unity-проекта.
        /// </summary>
        private static string ResolveProjectPath(string path)
        {
            if (Path.IsPathRooted(path))
                return path;

            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            return Path.GetFullPath(Path.Combine(projectRoot, path));
        }

        /// <summary>
        /// Убирает недопустимые символы из имени файла.
        /// </summary>
        private static string SanitizeFileName(string name)
        {
            char[] invalid = Path.GetInvalidFileNameChars();
            return new string(name.Where(c => !invalid.Contains(c)).ToArray());
        }
    }
}
#endif