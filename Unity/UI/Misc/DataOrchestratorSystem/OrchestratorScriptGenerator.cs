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
    /// - ReactiveValue наследники и ссылочные типы → SetData напрямую
    /// - int, float, bool → оборачиваются в IntData/FloatData/BoolData
    /// - enum → оборачивается в IntData с кастом к int
    /// - string → SetData напрямую (ссылочный тип)
    /// - Func, Action, делегаты → пропускаются
    /// - struct (не примитивы, не enum) → пропускаются
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
            if (name is "Value" or "State")
                return true;

            var type = p.PropertyType;

            // Func<>, Action<>, делегаты
            if (typeof(Delegate).IsAssignableFrom(type))
                return true;

            // struct (кроме примитивов и enum — они обрабатываются отдельно)
            if (type.IsValueType && !type.IsPrimitive && !type.IsEnum)
                return true;

            return false;
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
            var wrapperFields = new List<string>();
            var mapLines = new List<string>();
            var unmapLines = new List<string>();
            var updateLines = new List<string>();
            var hasWrappers = false;

            foreach (var prop in properties)
            {
                var propType = prop.PropertyType;
                var fieldName = ToCamelCase(prop.Name);
                fields.Add(new FieldEntry(fieldName, "DataStorage"));

                if (IsReactiveValue(propType))
                {
                    mapLines.Add($"        {fieldName}?.SetData(data.{prop.Name});");
                }
                else if (propType == typeof(int))
                {
                    usings.Add("Vortex.Core.Extensions.ReactiveValues");
                    var wrapper = $"_{fieldName}Value";
                    wrapperFields.Add($"    private IntData {wrapper} = new(0);");
                    mapLines.Add($"        {wrapper}.Set(data.{prop.Name});");
                    mapLines.Add($"        {fieldName}?.SetData({wrapper});");
                    unmapLines.Add($"        {wrapper}.Set(0);");
                    updateLines.Add($"        {wrapper}.Set(Data.{prop.Name});");
                    hasWrappers = true;
                }
                else if (propType == typeof(float))
                {
                    usings.Add("Vortex.Core.Extensions.ReactiveValues");
                    var wrapper = $"_{fieldName}Value";
                    wrapperFields.Add($"    private FloatData {wrapper} = new(0);");
                    mapLines.Add($"        {wrapper}.Set(data.{prop.Name});");
                    mapLines.Add($"        {fieldName}?.SetData({wrapper});");
                    unmapLines.Add($"        {wrapper}.Set(0);");
                    updateLines.Add($"        {wrapper}.Set(Data.{prop.Name});");
                    hasWrappers = true;
                }
                else if (propType == typeof(bool))
                {
                    usings.Add("Vortex.Core.Extensions.ReactiveValues");
                    var wrapper = $"_{fieldName}Value";
                    wrapperFields.Add($"    private BoolData {wrapper} = new(false);");
                    mapLines.Add($"        {wrapper}.Set(data.{prop.Name});");
                    mapLines.Add($"        {fieldName}?.SetData({wrapper});");
                    unmapLines.Add($"        {wrapper}.Set(false);");
                    updateLines.Add($"        {wrapper}.Set(Data.{prop.Name});");
                    hasWrappers = true;
                }
                else if (propType.IsEnum)
                {
                    usings.Add("Vortex.Core.Extensions.ReactiveValues");
                    var wrapper = $"_{fieldName}Value";
                    wrapperFields.Add($"    private IntData {wrapper} = new(0);");
                    mapLines.Add($"        {wrapper}.Set((int)data.{prop.Name});");
                    mapLines.Add($"        {fieldName}?.SetData({wrapper});");
                    unmapLines.Add($"        {wrapper}.Set(0);");
                    updateLines.Add($"        {wrapper}.Set((int)Data.{prop.Name});");
                    hasWrappers = true;
                }
                else if (propType.IsClass || propType.IsInterface || propType == typeof(string))
                {
                    mapLines.Add($"        {fieldName}?.SetData(data.{prop.Name});");
                }
                else
                {
                    mapLines.Add($"        // TODO: {prop.Name} ({GetFriendlyTypeName(propType)}) — требует ручной обёртки");
                }
            }

            if (!string.IsNullOrEmpty(dataNamespace))
                usings.Add(dataNamespace);

            // Usings
            foreach (var u in usings.OrderBy(s => s))
                sb.AppendLine($"using {u};");
            sb.AppendLine();

            // Class summary
            sb.AppendLine("/// <summary>");
            sb.AppendLine("/// Автогенерированный класс");
            sb.AppendLine("/// Оркестратор для данных. Позволяет быстро воспроизвести структуру данных на уровне GameObject");
            sb.AppendLine("/// и пробросить связи данные-контейнер ");
            sb.AppendLine("/// </summary>");
            sb.AppendLine($"public class {className} : DataOrchestrator<{dataClassName}>");
            sb.AppendLine("{");

            // TODO
            sb.AppendLine("    // TODO уникальную логику или ситуации нужно прописать отдельно ");
            sb.AppendLine();

            // DataStorage fields
            foreach (var f in fields)
                sb.AppendLine($"    [SerializeField] private {f.Type} {f.Name};");

            // Wrapper fields
            if (wrapperFields.Count > 0)
            {
                sb.AppendLine();
                foreach (var line in wrapperFields)
                    sb.AppendLine(line);
            }

            sb.AppendLine();

            // Map
            sb.AppendLine("    /// <summary>");
            sb.AppendLine("    /// Автогенерированный метод");
            sb.AppendLine("    /// ");
            sb.AppendLine("    /// Мэппинг данных по контейнерам");
            sb.AppendLine("    /// </summary>");
            sb.AppendLine("    /// <param name=\"data\"></param>");
            sb.AppendLine($"    protected override void Map({dataClassName} data)");
            sb.AppendLine("    {");
            foreach (var line in mapLines)
                sb.AppendLine(line);
            sb.AppendLine("    }");
            sb.AppendLine();

            // Unmap
            sb.AppendLine("    /// <summary>");
            sb.AppendLine("    /// Автогенерированный метод");
            sb.AppendLine("    /// ");
            sb.AppendLine("    /// Сброс данных упакованных в контейнер");
            sb.AppendLine("    /// </summary>");
            sb.AppendLine("    protected override void Unmap()");
            sb.AppendLine("    {");
            foreach (var line in unmapLines)
                sb.AppendLine(line);
            sb.AppendLine("    }");
            sb.AppendLine();

            // OnDataUpdate
            sb.AppendLine("    /// <summary>");
            sb.AppendLine("    /// Автогенерированный метод");
            sb.AppendLine("    ///");
            sb.AppendLine("    /// На обновление данных модели стоит провести обновление данных");
            sb.AppendLine("    /// упакованных в контейнер");
            sb.AppendLine("    /// </summary>");
            sb.AppendLine("    protected override void OnDataUpdate()");
            sb.AppendLine("    {");
            if (hasWrappers)
            {
                foreach (var line in updateLines)
                    sb.AppendLine(line);
            }
            sb.AppendLine("    }");

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
