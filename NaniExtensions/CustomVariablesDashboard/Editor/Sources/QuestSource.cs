using System;
using System.Collections.Generic;
using Vortex.NaniExtensions.CustomVariablesDashboard.Editor.Model;
#if USING_VORTEX_QUESTS
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
#endif

namespace Vortex.NaniExtensions.CustomVariablesDashboard.Editor.Sources
{
    /// <summary>
    /// Источник связей «квесты Vortex». Обходит все пресеты квестов проекта через сериализованные данные: стартовые
    /// условия, условия прерывания (с вложенными гейтами на любую глубину) и логики. Связь даёт каждый объект,
    /// реализующий <see cref="INaniVariableReference"/>; остальные пропускаются.
    ///
    /// Подключается только при <c>USING_VORTEX_QUESTS</c>; без него панель работает по сценариям <c>.nani</c>.
    /// </summary>
    internal static class QuestSource
    {
#if USING_VORTEX_QUESTS
        public static bool Enabled => true;

        /// <summary>Тип пресета квеста для поиска ассетов (<c>Vortex.Sdk.Quests.QuestPreset</c>)</summary>
        private const string PresetFilter = "t:QuestPreset";

        /// <summary>
        /// Сериализованные поля пресета квеста → раздел. Имена полей <c>QuestPreset</c>; поле вне списка (награды)
        /// не обходится.
        /// </summary>
        private static readonly Dictionary<string, QuestSection> Sections = new(StringComparer.Ordinal)
        {
            { "startConditions", QuestSection.Start },
            { "interruptConditions", QuestSection.Interrupt },
            { "logic", QuestSection.Logics }
        };

        private static readonly Regex Index = new(@"\[(\d+)\]");

        /// <summary>
        /// Собрать связи всех пресетов. Возвращает число просмотренных пресетов; -1 — сборку отменили.
        /// <paramref name="progress"/> получает долю и путь и возвращает true для отмены.
        /// </summary>
        public static int Collect(List<VariableLink> links, List<BuildProblem> problems, Func<float, string, bool> progress)
        {
            var guids = AssetDatabase.FindAssets(PresetFilter);
            // Квесты включены, а пресетов нет — почти наверняка переименован тип: молча отдать пустой источник нельзя,
            // иначе все переменные квестов уедут в «потеряшки»
            if (guids.Length == 0)
                problems.Add(new BuildProblem(LinkSource.Quest, string.Empty, 0,
                    $"Пресеты квестов не найдены ({PresetFilter}) — переименован тип пресета? Связи квестов не собраны"));

            for (var i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (progress(i / (float)guids.Length, path))
                    return -1;
                try
                {
                    CollectPreset(guids[i], path, links, problems);
                }
                catch (Exception e)
                {
                    problems.Add(new BuildProblem(LinkSource.Quest, path, 0, $"Пресет не обойдён: {e.Message}"));
                }
            }

            return guids.Length;
        }

        private static void CollectPreset(string guid, string path, List<VariableLink> links, List<BuildProblem> problems)
        {
            var preset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
            if (preset == null)
            {
                problems.Add(new BuildProblem(LinkSource.Quest, path, 0, "Пресет квеста не загружен"));
                return;
            }

            if (SerializationUtility.HasManagedReferencesWithMissingTypes(preset))
                foreach (var missing in SerializationUtility.GetManagedReferencesWithMissingTypes(preset))
                    problems.Add(new BuildProblem(LinkSource.Quest, path, 0,
                        $"Битая ссылка на тип {missing.namespaceName}.{missing.className} ({missing.assemblyName})"));

            var questName = Path.GetFileNameWithoutExtension(path);
            using var serialized = new SerializedObject(preset);

            // Шов с пакетом квестов: разделы ищутся по именам сериализованных полей пресета. Поле пропало —
            // структура пресета изменилась, связи раздела не собраны; сообщаем, а не теряем их молча
            foreach (var field in Sections.Keys)
                if (serialized.FindProperty(field) == null)
                    problems.Add(new BuildProblem(LinkSource.Quest, path, 0,
                        $"В пресете нет поля «{field}» — структура QuestPreset изменилась, связи раздела не собраны"));

            var property = serialized.GetIterator();
            while (property.Next(true))
            {
                if (property.propertyType != SerializedPropertyType.ManagedReference)
                    continue;

                var root = property.propertyPath.Split('.')[0];
                if (!Sections.TryGetValue(root, out var section))
                    continue;

                var value = property.managedReferenceValue;
                if (value == null)
                {
                    // Пустой тип — пустой элемент; непустой — битый тип, он уже в проблемах
                    if (string.IsNullOrEmpty(property.managedReferenceFullTypename))
                        problems.Add(new BuildProblem(LinkSource.Quest, path, 0,
                            $"Пустой элемент: {Describe(questName, section, property.propertyPath)}"));
                    continue;
                }

                if (value is INaniVariableReference reference)
                    CollectReference(reference, value, path, new QuestLocation(guid, questName, section,
                        GetPath(property.propertyPath)), links, problems);

                if (value is INaniExpressionReference expression)
                    CollectExpression(expression, path, new QuestLocation(guid, questName, section,
                        GetPath(property.propertyPath)), links, problems);
            }
        }

        /// <summary>Ссылка на одну переменную — одна связь с видом операции и операндом из контракта</summary>
        private static void CollectReference(INaniVariableReference reference, object value, string path,
            QuestLocation location, List<VariableLink> links, List<BuildProblem> problems)
        {
            if (string.IsNullOrWhiteSpace(reference.VariableName))
            {
                problems.Add(new BuildProblem(LinkSource.Quest, path, 0,
                    $"Ссылка на переменную с пустым именем: {location.Describe()}"));
                return;
            }

            links.Add(new VariableLink(reference.VariableName, LinkSource.Quest, reference.Operation, null,
                reference.Operand, $"{value.GetType().Name} {reference.Operand}", location));
        }

        /// <summary>
        /// Формула — разбор тем же разбором условий, что <c>if:</c> в сценариях: каждая переменная — своя связь со
        /// сравнением или чтением, место у всех — этот объект квеста.
        /// </summary>
        private static void CollectExpression(INaniExpressionReference expression, string path, QuestLocation location,
            List<VariableLink> links, List<BuildProblem> problems)
        {
            if (string.IsNullOrWhiteSpace(expression.Expression))
            {
                problems.Add(new BuildProblem(LinkSource.Quest, path, 0, $"Пустое выражение: {location.Describe()}"));
                return;
            }

            NaniScriptSource.CollectExpression(expression.Expression, LinkSource.Quest, location, links);
        }

        /// <summary>Индексы элементов массивов в пути свойства: <c>[группа, условие, вложенное…]</c> или <c>[логика]</c></summary>
        private static int[] GetPath(string propertyPath) =>
            Index.Matches(propertyPath).Select(m => int.Parse(m.Groups[1].Value)).ToArray();

        private static string Describe(string questName, QuestSection section, string propertyPath) =>
            new QuestLocation(string.Empty, questName, section, GetPath(propertyPath)).Describe();
#else
        public static bool Enabled => false;

        public static int Collect(List<VariableLink> links, List<BuildProblem> problems, Func<float, string, bool> progress) => 0;
#endif
    }
}
