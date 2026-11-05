using System;
using System.Collections.Generic;
using Vortex.NaniExtensions.NaniScriptLinks.Editor.Model;
#if USING_VORTEX_QUESTS
using System.IO;
using UnityEditor;
using UnityEngine;
#endif

namespace Vortex.NaniExtensions.NaniScriptLinks.Editor.Sources
{
    /// <summary>
    /// Источник «квесты Vortex». Обходит все пресеты квестов проекта через сериализованные данные, включая вложенные
    /// объекты. Связь-запуск даёт каждый объект, реализующий <see cref="INaniScriptLaunch"/>; остальные (в т.ч. те, что
    /// только ссылаются на сценарий) пропускаются. Пустой путь: ссылка на удалённый ассет сценария — битая ссылка,
    /// сценарий не назначен — проблема.
    ///
    /// Подключается только при <c>USING_VORTEX_QUESTS</c>; ссылки на сборку квестов нет — пресеты ищутся по имени типа.
    /// </summary>
    internal static class QuestSource
    {
#if USING_VORTEX_QUESTS
        public static bool Enabled => true;

        /// <summary>Тип пресета квеста для поиска ассетов (<c>Vortex.Sdk.Quests.QuestPreset</c>)</summary>
        private const string PresetFilter = "t:QuestPreset";

        /// <summary>Тип сериализованной ссылки на ассет сценария Naninovel</summary>
        private const string ScriptReferenceType = "PPtr<$Script>";

        /// <summary>
        /// Собрать запуски всех пресетов. Возвращает число просмотренных пресетов; -1 — сборку отменили.
        /// <paramref name="progress"/> получает долю и путь и возвращает true для отмены.
        /// </summary>
        public static int Collect(List<ScriptLink> links, List<BrokenLink> broken, List<BuildProblem> problems,
            Func<float, string, bool> progress)
        {
            var guids = AssetDatabase.FindAssets(PresetFilter);
            // Квесты включены, а пресетов нет — почти наверняка переименован тип: молча отдать пустой источник нельзя,
            // иначе все сценарии квестов уедут в сироты
            if (guids.Length == 0)
                problems.Add(new BuildProblem(
                    $"Пресеты квестов не найдены ({PresetFilter}) — переименован тип пресета? Запуски квестов не собраны",
                    new AssetLocation(string.Empty, "квесты Vortex")));

            for (var i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (progress(i / (float)guids.Length, path))
                    return -1;
                try
                {
                    CollectPreset(path, links, broken, problems);
                }
                catch (Exception e)
                {
                    problems.Add(new BuildProblem($"Пресет не обойдён: {e.Message}", new AssetLocation(path)));
                }
            }

            return guids.Length;
        }

        private static void CollectPreset(string path, List<ScriptLink> links, List<BrokenLink> broken,
            List<BuildProblem> problems)
        {
            var preset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
            if (preset == null)
            {
                problems.Add(new BuildProblem("Пресет квеста не загружен", new AssetLocation(path)));
                return;
            }

            var questName = Path.GetFileNameWithoutExtension(path);
            if (SerializationUtility.HasManagedReferencesWithMissingTypes(preset))
                foreach (var missing in SerializationUtility.GetManagedReferencesWithMissingTypes(preset))
                    problems.Add(new BuildProblem(
                        $"Битая ссылка на тип {missing.namespaceName}.{missing.className} ({missing.assemblyName}) — " +
                        "запуски из этого объекта не видны", new QuestLocation(path, questName, null)));

            using var serialized = new SerializedObject(preset);
            var property = serialized.GetIterator();
            while (property.Next(true))
            {
                if (property.propertyType != SerializedPropertyType.ManagedReference ||
                    property.managedReferenceValue is not INaniScriptLaunch launch)
                    continue;

                var location = new QuestLocation(path, questName,
                    $"{launch.GetType().Name} ({property.propertyPath})");
                var scriptPath = launch.ScriptPath;
                if (!string.IsNullOrEmpty(scriptPath))
                    links.Add(new ScriptLink(LinkSource.Quest, null, scriptPath, null, launch.GetType().Name, location));
                else if (HasMissingScript(property))
                    broken.Add(new BrokenLink(BrokenKind.MissingAsset, "Missing",
                        "Логика запускает удалённый ассет сценария", location));
                else
                    problems.Add(new BuildProblem("Логика-запуск без сценария — сценарий не назначен", location));
            }
        }

        /// <summary>
        /// Есть ли внутри объекта ссылка на ассет сценария, которая записана, но не разрешается (Missing): идентификатор
        /// объекта есть, самого объекта нет.
        /// </summary>
        private static bool HasMissingScript(SerializedProperty managedReference)
        {
            var child = managedReference.Copy();
            var end = managedReference.GetEndProperty();
            if (!child.Next(true))
                return false;
            while (!SerializedProperty.EqualContents(child, end))
            {
                if (child.propertyType == SerializedPropertyType.ObjectReference && child.type == ScriptReferenceType &&
                    child.objectReferenceValue == null && child.objectReferenceInstanceIDValue != 0)
                    return true;
                if (!child.Next(child.propertyType != SerializedPropertyType.ManagedReference))
                    break;
            }

            return false;
        }
#else
        public static bool Enabled => false;

        public static int Collect(List<ScriptLink> links, List<BrokenLink> broken, List<BuildProblem> problems,
            Func<float, string, bool> progress) => 0;
#endif
    }
}
