using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Vortex.NaniExtensions.NaniScriptLinks.Editor.Model
{
    /// <summary>Откуда пришла связь</summary>
    internal enum LinkSource
    {
        /// <summary>Команда в сценарии <c>.nani</c> (<c>@goto</c>, <c>@gosub</c>, переход из выбора)</summary>
        Script,

        /// <summary>Логика квеста, реализующая <see cref="INaniScriptLaunch"/></summary>
        Quest,

        /// <summary>Стартовый сценарий в настройках Naninovel</summary>
        Config
    }

    /// <summary>Вид битой ссылки</summary>
    internal enum BrokenKind
    {
        /// <summary>Цель-сценарий не найден в реестре</summary>
        NoScript,

        /// <summary>Сценарий есть, метки в нём нет</summary>
        NoLabel,

        /// <summary>Цель совпадает с существующей только без учёта регистра: в редакторе может работать, в сборке — нет</summary>
        CaseMismatch,

        /// <summary>Метка объявлена в сценарии больше одного раза — переход попадёт на первую</summary>
        DuplicateLabel,

        /// <summary>Логика квеста ссылается на удалённый ассет сценария</summary>
        MissingAsset
    }

    /// <summary>
    /// Место в проекте, откуда пришла связь или находка; умеет показать себя в редакторе.
    /// </summary>
    internal abstract class LinkLocation
    {
        /// <summary>Путь ассета в проекте (<c>Assets/…</c>, <c>ProjectSettings/…</c>)</summary>
        public string AssetPath { get; }

        protected LinkLocation(string assetPath) => AssetPath = assetPath;

        /// <summary>Подпись для таблиц окна</summary>
        public abstract string Describe();

        /// <summary>Открыть место: сценарий — на строке во внешнем редакторе, остальное — выделить в инспекторе</summary>
        public abstract void Open();

        /// <summary>Выделить ассет места: показать в инспекторе и подсветить в окне Project</summary>
        public void Find()
        {
            if (string.IsNullOrEmpty(AssetPath))
                return;
            var asset = Load();
            if (asset != null)
                Select(asset);
        }

        /// <summary>Загрузить ассет места; не найден — предупреждение (ассет перемещён или удалён после сборки)</summary>
        protected Object Load()
        {
            var asset = AssetDatabase.LoadMainAssetAtPath(AssetPath);
            if (asset == null)
                Debug.LogWarning($"[Nani Script Links] Ассет «{AssetPath}» не найден — перемещён или удалён после " +
                                 "сборки. Соберите карту заново.");
            return asset;
        }

        protected static void Select(Object asset)
        {
            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
        }
    }

    /// <summary>Строка сценария <c>.nani</c></summary>
    internal sealed class ScriptLocation : LinkLocation
    {
        /// <summary>Путь сценария в реестре Naninovel</summary>
        public string ScriptPath { get; }

        /// <summary>Номер строки, с 1; 0 — сценарий целиком</summary>
        public int Line { get; }

        public ScriptLocation(string scriptPath, string assetPath, int line) : base(assetPath)
        {
            ScriptPath = scriptPath;
            Line = line;
        }

        public override string Describe() => Line > 0 ? $"{ScriptPath}:{Line}" : ScriptPath;

        public override void Open()
        {
            var asset = Load();
            if (asset == null)
                return;
            if (Line > 0)
                AssetDatabase.OpenAsset(asset, Line);
            else
                Select(asset);
        }
    }

    /// <summary>Объект в пресете квеста</summary>
    internal sealed class QuestLocation : LinkLocation
    {
        public string QuestName { get; }

        /// <summary>Тип объекта квеста и его место в пресете (путь сериализованного свойства)</summary>
        public string Detail { get; }

        public QuestLocation(string assetPath, string questName, string detail) : base(assetPath)
        {
            QuestName = questName;
            Detail = detail;
        }

        public override string Describe() => string.IsNullOrEmpty(Detail) ? $"квест {QuestName}" : $"квест {QuestName} · {Detail}";

        public override void Open()
        {
            var asset = Load();
            if (asset != null)
                Select(asset);
        }
    }

    /// <summary>Ассет целиком: настройки Naninovel, реестр ресурсов, файл вне реестра</summary>
    internal sealed class AssetLocation : LinkLocation
    {
        /// <summary>Уточнение: поле настроек и т.п.; может быть пустым</summary>
        public string Detail { get; }

        public AssetLocation(string assetPath, string detail = null) : base(assetPath) => Detail = detail;

        public override string Describe()
        {
            if (string.IsNullOrEmpty(AssetPath))
                return Detail ?? string.Empty;
            return string.IsNullOrEmpty(Detail) ? AssetPath : $"{AssetPath} · {Detail}";
        }

        public override void Open()
        {
            if (string.IsNullOrEmpty(AssetPath))
                return;
            var asset = Load();
            if (asset != null)
                Select(asset);
        }
    }

    /// <summary>
    /// Связь — ссылка на точку входа сценария: откуда, куда и какой командой. Цель хранится как записана в источнике,
    /// переход внутри сценария (<c>.Label</c>) уже дополнен путём своего сценария.
    /// </summary>
    internal sealed class ScriptLink
    {
        public LinkSource Source { get; }

        /// <summary>Сценарий-источник (для <see cref="LinkSource.Script"/>); для квестов и настроек — null</summary>
        public string SourceScript { get; }

        /// <summary>Путь целевого сценария</summary>
        public string TargetScript { get; }

        /// <summary>Целевая метка; null — начало сценария</summary>
        public string TargetLabel { get; }

        /// <summary>Чем задана связь: <c>@goto</c>, <c>@choice goto:</c>, тип логики квеста, поле настроек</summary>
        public string Command { get; }

        public LinkLocation Location { get; }

        /// <summary>Цель в записи Naninovel: <c>Script</c> или <c>Script.Label</c></summary>
        public string Target => TargetLabel == null ? TargetScript : $"{TargetScript}.{TargetLabel}";

        public ScriptLink(LinkSource source, string sourceScript, string targetScript, string targetLabel, string command,
            LinkLocation location)
        {
            Source = source;
            SourceScript = sourceScript;
            TargetScript = targetScript;
            TargetLabel = targetLabel;
            Command = command;
            Location = location;
        }
    }

    /// <summary>
    /// Метка сценария — точка входа <c>{сценарий}.{метка}</c>: где объявлена и кто в неё переходит. Метка считается
    /// используемой при любой входящей связи, включая переход из своего сценария.
    /// </summary>
    internal sealed class LabelEntry
    {
        private readonly List<int> _lines = new();
        private readonly List<ScriptLink> _incoming = new();

        public string ScriptPath { get; }

        public string Name { get; }

        /// <summary>Ключ точки входа: <c>{сценарий}.{метка}</c></summary>
        public string Key => $"{ScriptPath}.{Name}";

        /// <summary>Строки объявления, с 1; больше одной — дубль</summary>
        public IReadOnlyList<int> Lines => _lines;

        public IReadOnlyList<ScriptLink> Incoming => _incoming;

        public LabelEntry(string scriptPath, string name)
        {
            ScriptPath = scriptPath;
            Name = name;
        }

        internal void AddDeclaration(int line) => _lines.Add(line);

        internal void AddIncoming(ScriptLink link) => _incoming.Add(link);
    }

    /// <summary>
    /// Сценарий из реестра Naninovel — точка входа <c>{сценарий}</c> со своими метками. Сценарий достижим, если есть
    /// хотя бы одна входящая связь извне: из другого сценария, квеста или настроек — на начало или на любую метку.
    /// Связь сценария на самого себя достижимости не даёт.
    /// </summary>
    internal sealed class ScriptEntry
    {
        private readonly Dictionary<string, LabelEntry> _labels = new(System.StringComparer.Ordinal);
        private readonly List<ScriptLink> _incoming = new();
        private readonly List<ScriptLink> _external = new();

        /// <summary>Путь в реестре: от Scenario Root, без расширения</summary>
        public string Path { get; }

        /// <summary>Путь файла в проекте</summary>
        public string AssetPath { get; }

        /// <summary>Метки по имени, с учётом регистра</summary>
        public IReadOnlyDictionary<string, LabelEntry> Labels => _labels;

        /// <summary>Связи на начало сценария (без метки), включая связи из самого сценария</summary>
        public IReadOnlyList<ScriptLink> Incoming => _incoming;

        /// <summary>Связи извне — на начало или на любую метку; пусто — сирота</summary>
        public IReadOnlyList<ScriptLink> ExternalIncoming => _external;

        public ScriptEntry(string path, string assetPath)
        {
            Path = path;
            AssetPath = assetPath;
        }

        internal LabelEntry GetOrAddLabel(string name)
        {
            if (!_labels.TryGetValue(name, out var label))
                _labels[name] = label = new LabelEntry(Path, name);
            return label;
        }

        /// <summary>Принять связь на сценарий: на начало или на объявленную метку</summary>
        internal void AddIncoming(ScriptLink link, LabelEntry label)
        {
            if (label != null)
                label.AddIncoming(link);
            else
                _incoming.Add(link);

            if (link.SourceScript != Path)
                _external.Add(link);
        }
    }

    /// <summary>Битая ссылка: связь или объявление, по которому переход не состоится или состоится не туда</summary>
    internal sealed class BrokenLink
    {
        public BrokenKind Kind { get; }

        /// <summary>Что записано в источнике: цель связи или ключ метки</summary>
        public string Target { get; }

        public string Message { get; }

        public LinkLocation Location { get; }

        public BrokenLink(BrokenKind kind, string target, string message, LinkLocation location)
        {
            Kind = kind;
            Target = target;
            Message = message;
            Location = location;
        }
    }

    /// <summary>Проблема сборки: то, что не удалось прочитать или разрешить</summary>
    internal sealed class BuildProblem
    {
        public string Message { get; }

        public LinkLocation Location { get; }

        public BuildProblem(string message, LinkLocation location)
        {
            Message = message;
            Location = location;
        }
    }
}
