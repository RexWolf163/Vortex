using System;
using UnityEditor;

namespace Vortex.NaniExtensions.CustomVariablesDashboard.Editor.Model
{
    /// <summary>
    /// Источник связи или проблемы сборки.
    /// </summary>
    internal enum LinkSource
    {
        /// <summary>Сценарий Naninovel</summary>
        Nani,

        /// <summary>Квест Vortex</summary>
        Quest,

        /// <summary>Конфигурация переменных Naninovel</summary>
        Config
    }

    /// <summary>
    /// Виды потерь переменной. Исключение (<see cref="VariableExclusion"/>) хранит набор этих флагов.
    /// </summary>
    [Flags]
    internal enum LossKinds
    {
        None = 0,

        /// <summary>Объявлена в конфиге, ни одной связи</summary>
        DeclaredUnused = 1 << 0,

        /// <summary>Есть связи, нет объявления</summary>
        UsedUndeclared = 1 << 1,

        /// <summary>Есть чтение/сравнение/ожидание, нет записи</summary>
        NeverWritten = 1 << 2,

        /// <summary>Есть запись, нет чтения/сравнения/ожидания</summary>
        NeverRead = 1 << 3
    }

    /// <summary>
    /// Переменная из <c>CustomVariablesConfiguration.PredefinedVariables</c>.
    /// </summary>
    internal sealed class VariableDeclaration
    {
        public string Name { get; }

        /// <summary>Начальное значение — выражение из конфига</summary>
        public string InitialValue { get; }

        public VariableDeclaration(string name, string initialValue)
        {
            Name = name;
            InitialValue = initialValue;
        }
    }

    /// <summary>
    /// Место связи. Вид места всегда соответствует источнику связи — связь и место создаются вместе одним источником.
    /// </summary>
    internal abstract class LinkLocation
    {
        /// <summary>Текст места для показа</summary>
        public abstract string Describe();

        /// <summary>Переход к месту</summary>
        public abstract void Open();
    }

    /// <summary>
    /// Место в сценарии <c>.nani</c>: путь ассета и строка.
    /// </summary>
    internal sealed class NaniLocation : LinkLocation
    {
        public string ScriptPath { get; }

        /// <summary>Номер строки, с 1</summary>
        public int Line { get; }

        public NaniLocation(string scriptPath, int line)
        {
            ScriptPath = scriptPath;
            Line = line;
        }

        public override string Describe() => $"{ScriptPath}:{Line}";

        public override void Open()
        {
            var asset = AssetDatabase.LoadMainAssetAtPath(ScriptPath);
            if (asset != null)
                AssetDatabase.OpenAsset(asset, Line);
        }
    }

    /// <summary>
    /// Раздел пресета квеста, в котором найдена связь.
    /// </summary>
    internal enum QuestSection
    {
        Start,
        Interrupt,
        Logics
    }

    /// <summary>
    /// Место в квесте Vortex: пресет и путь внутри него. Путь условий — <c>[группа, условие, вложенное…]</c> через
    /// вложенные гейты, путь логик — <c>[номер логики]</c>. Индексы с нуля, в показе — с единицы.
    /// </summary>
    internal sealed class QuestLocation : LinkLocation
    {
        public string PresetGuid { get; }

        /// <summary>Имя квеста на момент сборки</summary>
        public string QuestName { get; }

        public QuestSection Section { get; }

        public int[] Path { get; }

        public QuestLocation(string presetGuid, string questName, QuestSection section, int[] path)
        {
            PresetGuid = presetGuid;
            QuestName = questName;
            Section = section;
            Path = path;
        }

        public override string Describe() => $"{QuestName}: {DescribePath()}";

        /// <summary>«Старт › группа 2 › условие 1 › вложенное 3», «Прерывание › группа 1 › условие 2», «Логики › 4»</summary>
        public string DescribePath()
        {
            if (Section == QuestSection.Logics)
                return Path.Length > 0 ? $"Логики › {Path[0] + 1}" : "Логики";

            var text = Section == QuestSection.Start ? "Старт" : "Прерывание";
            for (var i = 0; i < Path.Length; i++)
                text += i switch
                {
                    0 => $" › группа {Path[i] + 1}",
                    1 => $" › условие {Path[i] + 1}",
                    _ => $" › вложенное {Path[i] + 1}"
                };
            return text;
        }

        public override void Open()
        {
            var asset = AssetDatabase.LoadMainAssetAtPath(AssetDatabase.GUIDToAssetPath(PresetGuid));
            if (asset == null)
                return;
            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
        }
    }

    /// <summary>
    /// Одно употребление переменной в одном месте.
    /// </summary>
    internal sealed class VariableLink
    {
        /// <summary>Имя переменной, как записано в источнике</summary>
        public string VariableName { get; }

        public LinkSource Source { get; }

        public NaniVariableOperation Operation { get; }

        /// <summary>Оператор: <c>=</c>, <c>++</c>, <c>&gt;=</c>…; пусто у чтения и прочего</summary>
        public string OperatorText { get; }

        /// <summary>Правая часть или операнд; может быть пустым</summary>
        public string Operand { get; }

        /// <summary>Исходное выражение или строка</summary>
        public string RawText { get; }

        public LinkLocation Location { get; }

        public VariableLink(string variableName, LinkSource source, NaniVariableOperation operation,
            string operatorText, string operand, string rawText, LinkLocation location)
        {
            VariableName = variableName;
            Source = source;
            Operation = operation;
            OperatorText = operatorText ?? string.Empty;
            Operand = operand ?? string.Empty;
            RawText = rawText ?? string.Empty;
            Location = location;
        }

        /// <summary>Чтение в смысле видов потерь: сравнение, ожидание и чтение</summary>
        public bool IsRead => Operation is NaniVariableOperation.Compare or NaniVariableOperation.Wait
            or NaniVariableOperation.Read;

        public bool IsWrite => Operation == NaniVariableOperation.Write;
    }

    /// <summary>
    /// То, что не удалось прочитать при сборке. Сборку не обрывает.
    /// </summary>
    internal sealed class BuildProblem
    {
        public LinkSource Source { get; }

        public string AssetPath { get; }

        /// <summary>Строка сценария, с 1; 0 — не применимо</summary>
        public int Line { get; }

        public string Message { get; }

        public BuildProblem(LinkSource source, string assetPath, int line, string message)
        {
            Source = source;
            AssetPath = assetPath;
            Line = line;
            Message = message;
        }

        public void Open()
        {
            var asset = AssetDatabase.LoadMainAssetAtPath(AssetPath);
            if (asset == null)
                return;
            if (Line > 0)
                AssetDatabase.OpenAsset(asset, Line);
            else
            {
                Selection.activeObject = asset;
                EditorGUIUtility.PingObject(asset);
            }
        }
    }
}
