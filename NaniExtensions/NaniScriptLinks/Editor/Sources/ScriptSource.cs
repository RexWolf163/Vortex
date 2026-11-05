using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Naninovel;
using UnityEditor;
using Vortex.NaniExtensions.NaniScriptLinks.Editor.Model;

namespace Vortex.NaniExtensions.NaniScriptLinks.Editor.Sources
{
    /// <summary>
    /// Источник «сценарии Naninovel». Список сценариев — реестр ресурсов Naninovel (<see cref="EditorResources"/>),
    /// путь — как в реестре. Метки и связи читаются из скомпилированного ассета сценария — того же разбора, по которому
    /// играет Naninovel (отступы, <c>if:</c>, флаги учтены им). Связь даёт каждый параметр команды с
    /// <see cref="EndpointContextAttribute"/> — так Naninovel сам помечает цели переходов (<c>@goto</c>, <c>@gosub</c>,
    /// <c>goto:</c>/<c>gosub:</c> у <c>@choice</c>); команды проекта с этим атрибутом подхватываются без правки
    /// валидатора. Условие перехода не вычисляется: условный переход — такая же связь.
    /// </summary>
    internal static class ScriptSource
    {
        /// <summary>Параметр команды — цель перехода, и как он записывается в сценарии</summary>
        private readonly struct EndpointField
        {
            public readonly FieldInfo Field;
            public readonly string Command;

            public EndpointField(FieldInfo field, string command)
            {
                Field = field;
                Command = command;
            }
        }

        private static readonly Dictionary<Type, EndpointField[]> FieldsByType = new();

        /// <summary>
        /// Сценарии реестра: запись → <see cref="ScriptEntry"/> по пути. Запись без файла, файл <c>.nani</c> вне
        /// реестра, незагружаемый ассет — в проблемы. Возвращает сценарии и их ассеты для разбора.
        /// </summary>
        public static Dictionary<string, ScriptEntry> CollectRegistry(List<BuildProblem> problems,
            Dictionary<string, Script> assets)
        {
            var config = Configuration.GetOrDefault<ScriptsConfiguration>();
            var prefix = config.Loader.PathPrefix;
            var resources = EditorResources.LoadOrDefault();
            var resourcesPath = AssetDatabase.GetAssetPath(resources);

            var scripts = new Dictionary<string, ScriptEntry>(StringComparer.Ordinal);
            var registered = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var record in resources.GetAllRecords(prefix).OrderBy(r => r.Key, StringComparer.Ordinal))
            {
                var path = record.Key.StartsWith(prefix + "/", StringComparison.Ordinal)
                    ? record.Key.Substring(prefix.Length + 1)
                    : record.Key;
                var assetPath = AssetDatabase.GUIDToAssetPath(record.Value);
                var script = string.IsNullOrEmpty(assetPath) ? null : AssetDatabase.LoadAssetAtPath<Script>(assetPath);
                if (script == null)
                {
                    problems.Add(new BuildProblem(
                        $"Запись реестра «{path}» без файла сценария (GUID {record.Value}) — файл удалён или не импортирован",
                        new AssetLocation(resourcesPath)));
                    continue;
                }

                registered.Add(assetPath);
                scripts[path] = new ScriptEntry(path, assetPath);
                assets[path] = script;
            }

            foreach (var assetPath in AssetDatabase.GetAllAssetPaths())
                if (assetPath.StartsWith("Assets/", StringComparison.Ordinal) &&
                    assetPath.EndsWith(".nani", StringComparison.OrdinalIgnoreCase) && !registered.Contains(assetPath))
                    problems.Add(new BuildProblem(
                        $"Сценарий не зарегистрирован в реестре Naninovel (вне Scenario Root «{config.ScenarioRoot}»?) — " +
                        "не анализируется, переходы на него не найдут цель", new AssetLocation(assetPath)));

            return scripts;
        }

        /// <summary>Метки и исходящие связи одного сценария</summary>
        public static void Collect(ScriptEntry entry, Script script, List<ScriptLink> links, List<BuildProblem> problems)
        {
            foreach (var line in script.Lines)
                switch (line)
                {
                    case LabelScriptLine label when !string.IsNullOrEmpty(label.LabelText):
                        entry.GetOrAddLabel(label.LabelText).AddDeclaration(label.LineNumber);
                        break;
                    case CommandScriptLine command:
                        CollectCommand(entry, command.Command, command.LineNumber, links, problems);
                        break;
                    case GenericTextScriptLine text:
                        foreach (var inlined in text.InlinedCommands)
                            CollectCommand(entry, inlined, text.LineNumber, links, problems);
                        break;
                }
        }

        private static void CollectCommand(ScriptEntry entry, Command command, int line, List<ScriptLink> links,
            List<BuildProblem> problems)
        {
            if (command == null)
                return;

            foreach (var endpoint in GetFields(command.GetType()))
            {
                if (endpoint.Field.GetValue(command) is not NamedStringParameter parameter || !parameter.HasValue)
                    continue;

                var location = new ScriptLocation(entry.Path, entry.AssetPath, line);
                if (parameter.DynamicValue)
                {
                    problems.Add(new BuildProblem(
                        $"{endpoint.Command} — цель задана выражением и статически не разрешается", location));
                    continue;
                }

                var target = string.IsNullOrEmpty(parameter.Name) ? entry.Path : parameter.Name;
                var namedValue = parameter.NamedValue;
                var label = namedValue != null && namedValue.HasValue && !string.IsNullOrEmpty(namedValue.Value)
                    ? namedValue.Value
                    : null;
                links.Add(new ScriptLink(LinkSource.Script, entry.Path, target, label, endpoint.Command, location));
            }
        }

        private static EndpointField[] GetFields(Type commandType)
        {
            if (FieldsByType.TryGetValue(commandType, out var fields))
                return fields;

            var alias = commandType.GetCustomAttribute<Command.CommandAliasAttribute>()?.Alias;
            if (string.IsNullOrEmpty(alias))
                alias = char.ToLowerInvariant(commandType.Name[0]) + commandType.Name.Substring(1);

            fields = commandType.GetFields(BindingFlags.Public | BindingFlags.Instance)
                .Where(f => typeof(NamedStringParameter).IsAssignableFrom(f.FieldType) &&
                            f.IsDefined(typeof(EndpointContextAttribute), true))
                .Select(f =>
                {
                    var parameter = f.GetCustomAttribute<Command.ParameterAliasAttribute>()?.Alias ?? f.Name;
                    return new EndpointField(f, string.IsNullOrEmpty(parameter) ? $"@{alias}" : $"@{alias} {parameter}:");
                })
                .ToArray();
            FieldsByType[commandType] = fields;
            return fields;
        }
    }
}
