#if UNITY_EDITOR
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace Vortex.Unity.Extensions.Editor
{
    /// <summary>
    /// Генератор реактивной обёртки для класса.
    /// Правый клик по .cs-скрипту в Project → Vortex → Create Reactive Wrapper.
    /// Создаёт рядом файл {ClassName}Data.cs с классом-наследником ReactiveValue&lt;ClassName&gt;.
    /// Namespace берётся из исходного класса.
    /// </summary>
    internal static class ReactiveWrapperGenerator
    {
        private const string MenuPath = "Assets/Create/Vortex Templates/Create ReactiveData";
        private const int MenuPriority = 80;

        private const string WrapperSuffix = "Data";
        private const string FileExtension = ".cs";
        private const string BaseNamespace = "Vortex.Core.Extensions.ReactiveValues";

        [MenuItem(MenuPath, false, MenuPriority)]
        private static void Generate()
        {
            var script = GetSelectedMonoScript();
            if (script == null)
                return;

            var scriptPath = AssetDatabase.GetAssetPath(script);
            var folder = Path.GetDirectoryName(scriptPath);
            if (string.IsNullOrEmpty(folder))
                return;

            ResolveTypeInfo(script, out var className, out var ns);
            if (string.IsNullOrEmpty(className))
            {
                EditorUtility.DisplayDialog("Reactive Wrapper",
                    "Не удалось определить имя класса из выбранного скрипта.", "OK");
                return;
            }

            var wrapperName = className + WrapperSuffix;
            var outPath = Path.Combine(folder, wrapperName + FileExtension);

            if (File.Exists(outPath) &&
                !EditorUtility.DisplayDialog("Reactive Wrapper",
                    $"Файл {wrapperName}{FileExtension} уже существует. Перезаписать?",
                    "Перезаписать", "Отмена"))
                return;

            File.WriteAllText(outPath, BuildContent(className, wrapperName, ns));
            AssetDatabase.Refresh();

            var asset = AssetDatabase.LoadAssetAtPath<MonoScript>(ToAssetPath(outPath));
            if (asset == null)
                return;
            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
        }

        [MenuItem(MenuPath, true)]
        private static bool Validate() => GetSelectedMonoScript() != null;

        /// <summary>
        /// Единственный выбранный .cs-скрипт, либо null.
        /// </summary>
        private static MonoScript GetSelectedMonoScript()
        {
            var scripts = Selection.GetFiltered<MonoScript>(SelectionMode.Assets);
            return scripts.Length == 1 ? scripts[0] : null;
        }

        /// <summary>
        /// Имя класса и namespace. Через рефлексию, если тип разрешается;
        /// иначе — из текста скрипта (POCO часто не резолвятся через GetClass).
        /// </summary>
        private static void ResolveTypeInfo(MonoScript script, out string className, out string ns)
        {
            var type = script.GetClass();
            if (type != null)
            {
                className = type.Name;
                ns = type.Namespace;
                return;
            }

            className = script.name;
            ns = ParseNamespace(script.text);
        }

        /// <summary>
        /// Первое объявление namespace (блочное или file-scoped). null для глобального.
        /// </summary>
        private static string ParseNamespace(string source)
        {
            if (string.IsNullOrEmpty(source))
                return null;
            var match = Regex.Match(source, @"(?m)^\s*namespace\s+([A-Za-z_][\w.]*)");
            return match.Success ? match.Groups[1].Value : null;
        }

        private static string BuildContent(string className, string wrapperName, string ns)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"using {BaseNamespace};");
            sb.AppendLine();

            var hasNs = !string.IsNullOrEmpty(ns);
            var ind = hasNs ? "    " : "";

            if (hasNs)
            {
                sb.AppendLine($"namespace {ns}");
                sb.AppendLine("{");
            }

            sb.AppendLine($"{ind}public class {wrapperName} : ReactiveValue<{className}>");
            sb.AppendLine($"{ind}{{");
            sb.AppendLine($"{ind}    public {wrapperName}({className} value) => Value = value;");
            sb.AppendLine();
            sb.AppendLine($"{ind}    public {wrapperName}({className} value, object owner)");
            sb.AppendLine($"{ind}    {{");
            sb.AppendLine($"{ind}        Value = value;");
            sb.AppendLine($"{ind}        _owner = owner;");
            sb.AppendLine($"{ind}    }}");
            sb.AppendLine($"{ind}}}");

            if (hasNs)
                sb.AppendLine("}");

            return sb.ToString();
        }

        private static string ToAssetPath(string fullPath)
        {
            var normalized = fullPath.Replace('\\', '/');
            var dataPath = Application.dataPath.Replace('\\', '/');
            return normalized.StartsWith(dataPath)
                ? "Assets" + normalized.Substring(dataPath.Length)
                : normalized;
        }
    }
}
#endif
