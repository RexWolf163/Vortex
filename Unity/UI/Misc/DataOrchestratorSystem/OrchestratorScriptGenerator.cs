#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using Vortex.Core.Extensions.ReactiveValues;

namespace Vortex.Unity.UI.Misc.DataOrchestratorSystem
{
    /// <summary>
    /// Генератор DataOrchestrator-наследника по модели данных.
    /// ПКМ по .cs файлу → Create → Vortex Templates → DataOrchestrator
    ///
    /// Контракт:
    /// - ReactiveValue наследники и ссылочные типы → Push напрямую
    /// - Значимые типы (int, float, bool, etc.) → оборачиваются в IntData/FloatData/BoolData/StringData
    /// - Прочие значимые типы → generic обёртка ReactiveValue (TODO: ручная правка)
    /// </summary>
    internal static class OrchestratorScriptGenerator
    {
        private const string MenuPath = "Assets/Create/Vortex Templates/DataOrchestrator";
        private const int MenuPriority = 83;

        [MenuItem(MenuPath, priority = MenuPriority)]
        private static void CreateOrchestratorScript()
        {
            var monoScript = Selection.activeObject as MonoScript;
            if (monoScript == null)
                return;

            var dataType = monoScript.GetClass();
            if (dataType == null)
                return;

            var dataClassName = dataType.Name;
            var orchestratorName = dataClassName + "Orchestrator";

            var scriptPath = AssetDatabase.GetAssetPath(monoScript);
            var targetFolder = Path.GetDirectoryName(scriptPath) ?? Application.dataPath;

            var fullPath = GenerateUniqueFilePath(targetFolder, orchestratorName);

            var properties = ExtractProperties(dataType);
            var content = GenerateScript(orchestratorName, dataClassName, dataType.Namespace, properties);

            File.WriteAllText(fullPath, content);
            AssetDatabase.Refresh();

            var asset = AssetDatabase.LoadAssetAtPath<MonoScript>(ToAssetPath(fullPath));
            if (asset != null)
            {
                Selection.activeObject = asset;
                EditorGUIUtility.PingObject(asset);
            }
        }

        [MenuItem(MenuPath, validate = true)]
        private static bool Validate()
        {
            var monoScript = Selection.activeObject as MonoScript;
            if (monoScript == null)
                return false;
            var type = monoScript.GetClass();
            return type != null && !type.IsAbstract && type.IsClass;
        }

        private static PropertyInfo[] ExtractProperties(Type type)
        {
            return type
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanRead && p.GetMethod.IsPublic)
                .Where(p => p.DeclaringType == type)
                .Where(p => !IsExcluded(p))
                .ToArray();
        }

        private static bool IsExcluded(PropertyInfo p)
        {
            var name = p.Name;
            return name is "Value" or "State";
        }

        private static string GenerateScript(
            string className,
            string dataClassName,
            string dataNamespace,
            PropertyInfo[] properties)
        {
            var sb = new StringBuilder();
            var usings = new HashSet<string>
            {
                "UnityEngine",
                "Vortex.Unity.UI.Misc",
                "Vortex.Unity.UI.Misc.DataOrchestratorSystem"
            };

            var fields = new List<FieldEntry>();
            var mapLines = new List<string>();
            var unmapLines = new List<string>();
            var needsOnUpdated = false;
            var extraFields = new List<string>();

            foreach (var prop in properties)
            {
                var propType = prop.PropertyType;
                var fieldName = ToCamelCase(prop.Name);

                if (IsReactiveValue(propType))
                {
                    fields.Add(new FieldEntry(fieldName, "DataStorage"));
                    mapLines.Add($"            Push({fieldName}, data.{prop.Name});");
                }
                else if (propType.IsClass || propType.IsInterface)
                {
                    fields.Add(new FieldEntry(fieldName, "DataStorage"));
                    mapLines.Add($"            Push({fieldName}, data.{prop.Name});");
                }
                else if (propType == typeof(int))
                {
                    usings.Add("Vortex.Core.Extensions.ReactiveValues");
                    fields.Add(new FieldEntry(fieldName, "DataStorage"));
                    var wrapperField = $"_{fieldName}Value";
                    extraFields.Add($"        private IntData {wrapperField} = new(0);");
                    mapLines.Add($"            {wrapperField}.Set(data.{prop.Name});");
                    mapLines.Add($"            Push({fieldName}, {wrapperField});");
                    needsOnUpdated = true;
                    unmapLines.Add($"            {wrapperField}.Set(0);");
                }
                else if (propType == typeof(float))
                {
                    usings.Add("Vortex.Core.Extensions.ReactiveValues");
                    fields.Add(new FieldEntry(fieldName, "DataStorage"));
                    var wrapperField = $"_{fieldName}Value";
                    extraFields.Add($"        private FloatData {wrapperField} = new(0);");
                    mapLines.Add($"            {wrapperField}.Set(data.{prop.Name});");
                    mapLines.Add($"            Push({fieldName}, {wrapperField});");
                    needsOnUpdated = true;
                    unmapLines.Add($"            {wrapperField}.Set(0);");
                }
                else if (propType == typeof(bool))
                {
                    usings.Add("Vortex.Core.Extensions.ReactiveValues");
                    fields.Add(new FieldEntry(fieldName, "DataStorage"));
                    var wrapperField = $"_{fieldName}Value";
                    extraFields.Add($"        private BoolData {wrapperField} = new(false);");
                    mapLines.Add($"            {wrapperField}.Set(data.{prop.Name});");
                    mapLines.Add($"            Push({fieldName}, {wrapperField});");
                    needsOnUpdated = true;
                    unmapLines.Add($"            {wrapperField}.Set(false);");
                }
                else if (propType == typeof(string))
                {
                    usings.Add("Vortex.Core.Extensions.ReactiveValues");
                    fields.Add(new FieldEntry(fieldName, "DataStorage"));
                    var wrapperField = $"_{fieldName}Value";
                    extraFields.Add($"        private StringData {wrapperField} = new(string.Empty);");
                    mapLines.Add($"            {wrapperField}.Set(data.{prop.Name});");
                    mapLines.Add($"            Push({fieldName}, {wrapperField});");
                    needsOnUpdated = true;
                    unmapLines.Add($"            {wrapperField}.Set(string.Empty);");
                }
                else
                {
                    // Прочие значимые типы — помечаем TODO
                    fields.Add(new FieldEntry(fieldName, "DataStorage"));
                    mapLines.Add($"            // TODO: {prop.Name} ({GetFriendlyTypeName(propType)}) — требует ручной обёртки");
                }
            }

            if (!string.IsNullOrEmpty(dataNamespace))
                usings.Add(dataNamespace);

            // Write usings
            foreach (var u in usings.OrderBy(s => s))
                sb.AppendLine($"using {u};");
            sb.AppendLine();

            // Namespace — берём из модели данных, если есть
            var ns = !string.IsNullOrEmpty(dataNamespace) ? dataNamespace : null;
            if (ns != null)
            {
                sb.AppendLine($"namespace {ns}");
                sb.AppendLine("{");
            }

            var indent = ns != null ? "    " : "";

            // Class
            sb.AppendLine($"{indent}public class {className} : DataOrchestrator<{dataClassName}>");
            sb.AppendLine($"{indent}{{");

            // DataStorage fields
            foreach (var f in fields)
                sb.AppendLine($"{indent}    [SerializeField] private {f.Type} {f.Name};");

            if (fields.Count > 0 && extraFields.Count > 0)
                sb.AppendLine();

            // Wrapper fields
            foreach (var line in extraFields)
                sb.AppendLine($"{indent}{line}");

            sb.AppendLine();

            // Map
            sb.AppendLine($"{indent}    protected override void Map({dataClassName} data)");
            sb.AppendLine($"{indent}    {{");
            foreach (var line in mapLines)
                sb.AppendLine($"{indent}{line}");
            sb.AppendLine($"{indent}    }}");
            sb.AppendLine();

            // Unmap
            sb.AppendLine($"{indent}    protected override void Unmap()");
            sb.AppendLine($"{indent}    {{");
            foreach (var line in unmapLines)
                sb.AppendLine($"{indent}{line}");
            sb.AppendLine($"{indent}    }}");
            sb.AppendLine();

            // Subscribe
            sb.AppendLine($"{indent}    protected override void Subscribe({dataClassName} data)");
            sb.AppendLine($"{indent}    {{");
            sb.AppendLine($"{indent}        // TODO: подпишитесь на события модели данных");
            if (needsOnUpdated)
                sb.AppendLine($"{indent}        // data.OnUpdated += OnDataUpdated;");
            sb.AppendLine($"{indent}    }}");
            sb.AppendLine();

            // Unsubscribe
            sb.AppendLine($"{indent}    protected override void Unsubscribe({dataClassName} data)");
            sb.AppendLine($"{indent}    {{");
            sb.AppendLine($"{indent}        // TODO: отпишитесь от событий модели данных");
            if (needsOnUpdated)
                sb.AppendLine($"{indent}        // data.OnUpdated -= OnDataUpdated;");
            sb.AppendLine($"{indent}    }}");

            // OnDataUpdated
            if (needsOnUpdated)
            {
                sb.AppendLine();
                sb.AppendLine($"{indent}    protected override void OnDataUpdated()");
                sb.AppendLine($"{indent}    {{");

                foreach (var prop in properties)
                {
                    var propType = prop.PropertyType;
                    if (propType != typeof(int) && propType != typeof(float)
                        && propType != typeof(bool) && propType != typeof(string))
                        continue;

                    var fieldName = ToCamelCase(prop.Name);
                    var wrapperField = $"_{fieldName}Value";
                    sb.AppendLine($"{indent}        {wrapperField}.Set(Data.{prop.Name});");
                }

                sb.AppendLine($"{indent}    }}");
            }

            sb.AppendLine($"{indent}}}");

            if (ns != null)
                sb.AppendLine("}");

            return sb.ToString();
        }

        private static bool IsReactiveValue(Type type)
        {
            var current = type;
            while (current != null)
            {
                if (current.IsGenericType && current.GetGenericTypeDefinition() == typeof(ReactiveValue<>))
                    return true;
                current = current.BaseType;
            }

            if (typeof(IReactiveData).IsAssignableFrom(type))
                return true;

            return false;
        }

        private static string ToCamelCase(string name)
        {
            if (string.IsNullOrEmpty(name))
                return name;
            return char.ToLowerInvariant(name[0]) + name.Substring(1);
        }

        private static string GetFriendlyTypeName(Type type)
        {
            if (type == typeof(int)) return "int";
            if (type == typeof(float)) return "float";
            if (type == typeof(double)) return "double";
            if (type == typeof(bool)) return "bool";
            if (type == typeof(string)) return "string";
            if (type == typeof(long)) return "long";
            if (type == typeof(byte)) return "byte";
            return type.Name;
        }

        private static string GenerateUniqueFilePath(string folder, string baseName)
        {
            var candidate = Path.Combine(folder, baseName + ".cs");
            if (!File.Exists(candidate))
                return candidate;

            var counter = 1;
            while (File.Exists(candidate))
            {
                candidate = Path.Combine(folder, $"{baseName}{counter}.cs");
                counter++;
            }

            return candidate;
        }

        private static string ToAssetPath(string fullPath)
        {
            var normalized = fullPath.Replace('\\', '/');
            var dataPath = Application.dataPath.Replace('\\', '/');

            if (normalized.StartsWith(dataPath))
                return "Assets" + normalized.Substring(dataPath.Length);

            return normalized;
        }

        private struct FieldEntry
        {
            public readonly string Name;
            public readonly string Type;

            public FieldEntry(string name, string type)
            {
                Name = name;
                Type = type;
            }
        }
    }
}
#endif
