#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using Vortex.Core.DatabaseSystem.Model;
using Vortex.Core.Extensions.LogicExtensions;

namespace Vortex.Unity.DatabaseSystemEditor.Editor
{
    /// <summary>
    /// Генератор Preset-скриптов для существующих Record.
    /// Доступен через контекстное меню Project: ПКМ по .cs файлу Record > Create Preset for Record
    ///
    /// Контракт генерации свойств:
    /// - List&lt;T&gt; в Record → T[] поле в Preset, свойство возвращает new List&lt;T&gt;(field) или с DeepCopy элементов для ссылочных T
    /// - Примитивные и платформенные типы (ObjectExtDeepClone.IsImmutable) → прямой возврат
    /// - Прочие ссылочные типы → свойство возвращает field.DeepCopy() (гарантия иммутабельности)
    ///
    /// AI-код
    /// </summary>
    internal static class RecordPresetScriptGenerator
    {
        private const string MenuPath = "Assets/Create/Vortex Templates/Preset for Record";
        private const int MenuPriority = 82;

        private const string FileExtension = ".cs";

        [MenuItem(MenuPath, priority = MenuPriority)]
        private static void CreatePresetScript()
        {
            var monoScript = Selection.activeObject as MonoScript;
            if (monoScript == null)
                return;

            var recordType = monoScript.GetClass();
            if (recordType == null)
                return;

            var recordClassName = recordType.Name;
            var presetClassName = recordClassName + "Preset";

            var scriptPath = AssetDatabase.GetAssetPath(monoScript);
            var targetFolder = Path.GetDirectoryName(scriptPath) ?? Application.dataPath;

            var fullPath = GenerateUniqueFilePath(targetFolder, presetClassName);

            var properties = ExtractCopyableProperties(recordType);
            var content = GenerateScriptContent(presetClassName, recordClassName, recordType.Namespace, properties);

            File.WriteAllText(fullPath, content);
            AssetDatabase.Refresh();

            var asset = AssetDatabase.LoadAssetAtPath<MonoScript>(ToAssetPath(fullPath));
            if (asset == null)
                return;
            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
        }

        [MenuItem(MenuPath, validate = true)]
        private static bool ValidateCreatePresetScript()
        {
            var monoScript = Selection.activeObject as MonoScript;
            if (monoScript == null)
                return false;

            var scriptClass = monoScript.GetClass();
            if (scriptClass == null)
                return false;

            return IsRecordType(scriptClass);
        }

        private static bool IsRecordType(Type type)
        {
            if (type == null)
                return false;

            var current = type.BaseType;
            while (current != null)
            {
                if (current == typeof(Record))
                    return true;
                current = current.BaseType;
            }

            return false;
        }

        private static PropertyInfo[] ExtractCopyableProperties(Type recordType)
        {
            // CopyFrom копирует свойства с публичным getter
            // Исключаем базовые свойства Record (GuidPreset, Name, Description)
            var baseProps = new[] { "GuidPreset", "Name", "Description" };

            return recordType
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanRead && p.GetMethod.IsPublic)
                .Where(p => !baseProps.Contains(p.Name))
                .Where(p => p.DeclaringType != typeof(Record) && p.DeclaringType != typeof(object))
                .ToArray();
        }

        private static string GenerateScriptContent(
            string presetClassName,
            string recordClassName,
            string recordNamespace,
            PropertyInfo[] properties)
        {
            var sb = new StringBuilder();

            // Usings
            var needsDeepCopy = properties.Any(p => NeedsDeepCopy(p.PropertyType));
            var needsListConversion = properties.Any(p => IsListType(p.PropertyType));

            sb.AppendLine("using UnityEngine;");
            sb.AppendLine("using Vortex.Unity.DatabaseSystem.Presets;");
            if (needsDeepCopy)
                sb.AppendLine("using Vortex.Core.Extensions.LogicExtensions;");
            if (needsListConversion)
                sb.AppendLine("using System.Collections.Generic;");
            if (!string.IsNullOrEmpty(recordNamespace))
                sb.AppendLine($"using {recordNamespace};");
            sb.AppendLine();

            // Class
            sb.AppendLine(
                $"[CreateAssetMenu(fileName = \"{recordClassName}\", menuName = \"Database/{recordClassName}\")]");
            sb.AppendLine($"public class {presetClassName} : RecordPreset<{recordClassName}>");
            sb.AppendLine("{");

            // Properties
            if (properties.Length > 0)
            {
                sb.AppendLine("    // Свойства для CopyFrom - имена должны совпадать с Record");
                sb.AppendLine();

                foreach (var prop in properties)
                {
                    var fieldName = ToCamelCase(prop.Name);
                    var propType = prop.PropertyType;
                    var propTypeName = GetFriendlyTypeName(propType);

                    if (IsListType(propType))
                    {
                        var elementType = propType.GetGenericArguments()[0];
                        var elementTypeName = GetFriendlyTypeName(elementType);
                        var needsElementCopy = NeedsDeepCopy(elementType);

                        sb.AppendLine($"    [SerializeField]");
                        sb.AppendLine($"    private {elementTypeName}[] {fieldName};");
                        sb.AppendLine();

                        if (needsElementCopy)
                            sb.AppendLine($"    public {propTypeName} {prop.Name} => new {propTypeName}(System.Array.ConvertAll({fieldName}, e => e.DeepCopy()));");
                        else
                            sb.AppendLine($"    public {propTypeName} {prop.Name} => new {propTypeName}({fieldName});");
                    }
                    else if (propType.IsArray && ObjectExtDeepClone.IsImmutable(propType.GetElementType()))
                    {
                        sb.AppendLine($"    [SerializeField]");
                        sb.AppendLine($"    private {propTypeName} {fieldName};");
                        sb.AppendLine();
                        sb.AppendLine($"    public {propTypeName} {prop.Name} => ({propTypeName}){fieldName}.Clone();");
                    }
                    else if (NeedsDeepCopy(propType))
                    {
                        sb.AppendLine($"    [SerializeField]");
                        sb.AppendLine($"    private {propTypeName} {fieldName};");
                        sb.AppendLine();
                        sb.AppendLine($"    public {propTypeName} {prop.Name} => {fieldName}.DeepCopy();");
                    }
                    else
                    {
                        sb.AppendLine($"    [SerializeField]");
                        sb.AppendLine($"    private {propTypeName} {fieldName};");
                        sb.AppendLine();
                        sb.AppendLine($"    public {propTypeName} {prop.Name} => {fieldName};");
                    }

                    sb.AppendLine();
                }
            }
            else
            {
                sb.AppendLine("    // TODO: Добавить свойства, соответствующие Record");
                sb.AppendLine("    // [SerializeField] private int someValue;");
                sb.AppendLine("    // public int SomeValue => someValue;");
            }

            sb.AppendLine(OnValidateBlock());
            sb.AppendLine("}");

            return sb.ToString();
        }

        private static string OnValidateBlock()
        {
            return
                "#if UNITY_EDITOR\n    //Раскомментируйте нужное при необходимости \n    //private void OnValidate() => type = RecordTypes.Singleton;\n    //private void OnValidate() => type = RecordTypes.MultiInstance;\n#endif";
        }

        private static bool IsListType(Type type)
        {
            return type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>);
        }

        private static bool NeedsDeepCopy(Type type)
        {
            if (IsListType(type))
                return false; // List обрабатывается отдельно через Array

            if (type.IsArray)
                return true;

            if (ObjectExtDeepClone.IsImmutable(type))
                return false;

            if (type.IsValueType)
                return false;

            return true;
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

            if (type.IsGenericType)
            {
                var genericDef = type.GetGenericTypeDefinition();
                var genericArgs = string.Join(", ", type.GetGenericArguments().Select(GetFriendlyTypeName));

                if (genericDef == typeof(System.Collections.Generic.List<>))
                    return $"List<{genericArgs}>";
                if (genericDef == typeof(System.Collections.Generic.Dictionary<,>))
                    return $"Dictionary<{genericArgs}>";

                return $"{type.Name.Split('`')[0]}<{genericArgs}>";
            }

            if (type.IsArray)
                return $"{GetFriendlyTypeName(type.GetElementType())}[]";

            return type.Name;
        }

        private static string GenerateUniqueFilePath(string folder, string baseName)
        {
            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);

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