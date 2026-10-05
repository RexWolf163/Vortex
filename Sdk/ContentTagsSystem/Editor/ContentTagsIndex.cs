#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Vortex.Sdk.ContentTagsSystem.Handlers;

namespace Vortex.Sdk.ContentTagsSystem.Editor
{
    /// <summary>Где найден носитель тега.</summary>
    internal enum UsageKind
    {
        Prefab,
        OpenScene,
        ClosedScene
    }

    /// <summary>Один носитель тега: компонент на префабе или сцене.</summary>
    internal sealed class TagUsage
    {
        public string Tag;

        public UsageKind Kind;

        /// <summary>Путь ассета: префаба или сцены.</summary>
        public string AssetPath;

        /// <summary>Путь объекта внутри иерархии. Для закрытой сцены не определяется.</summary>
        public string ObjectPath;

        /// <summary>Имя компонента-носителя. Для закрытой сцены не определяется.</summary>
        public string Component;

        /// <summary>Прямая ссылка для открытой сцены; гаснет при закрытии сцены.</summary>
        public GameObject SceneObject;
    }

    /// <summary>
    /// Индекс использования тегов по проекту. Один проход собирает носителей сразу по всем тегам —
    /// выпадашка окна потом только фильтрует вывод, пересканирование ей не нужно.
    ///
    /// Префабы и открытые сцены дают носителя и переход к нему. Закрытые сцены разбираются текстом:
    /// открывать каждую сцену ради ревизии слишком дорого, поэтому по ним известен только факт
    /// использования тега.
    /// </summary>
    internal static class ContentTagsIndex
    {
        private const string Title = "Content Tags";

        /// <summary>Разбор сцен возможен только при текстовой сериализации.</summary>
        internal static bool ScenesReadable =>
            EditorSettings.serializationMode == SerializationMode.ForceText;

        /// <summary>
        /// Собрать индекс «тег → носители». Возвращает <c>null</c>, если пользователь отменил проход.
        /// </summary>
        internal static Dictionary<string, List<TagUsage>> Build()
        {
            var index = new Dictionary<string, List<TagUsage>>();
            try
            {
                if (!ScanPrefabs(index))
                    return null;
                ScanOpenScenes(index);
                if (!ScanClosedScenes(index))
                    return null;
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            return index;
        }

        private static bool ScanPrefabs(IDictionary<string, List<TagUsage>> index)
        {
            var guids = AssetDatabase.FindAssets("t:Prefab");
            for (var i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (EditorUtility.DisplayCancelableProgressBar(Title,
                        $"Префабы: {path}", (float)i / guids.Length))
                    return false;

                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                    continue;

                CollectFrom(prefab, UsageKind.Prefab, path, index, false);
            }

            return true;
        }

        private static void ScanOpenScenes(IDictionary<string, List<TagUsage>> index)
        {
            for (var i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded)
                    continue;

                foreach (var root in scene.GetRootGameObjects())
                    CollectFrom(root, UsageKind.OpenScene, scene.path, index, true);
            }
        }

        private static bool ScanClosedScenes(IDictionary<string, List<TagUsage>> index)
        {
            if (!ScenesReadable)
                return true;

            var open = new HashSet<string>();
            for (var i = 0; i < SceneManager.sceneCount; i++)
                open.Add(SceneManager.GetSceneAt(i).path);

            var scriptGuids = ScriptGuids();
            if (scriptGuids.Count == 0)
                return true;

            var guids = AssetDatabase.FindAssets("t:Scene");
            for (var i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (open.Contains(path))
                    continue;

                if (EditorUtility.DisplayCancelableProgressBar(Title,
                        $"Сцены: {path}", (float)i / guids.Length))
                    return false;

                string text;
                try
                {
                    text = File.ReadAllText(path);
                }
                catch (Exception exception)
                {
                    Debug.LogWarning($"[ContentTags] Сцена «{path}» не прочитана: {exception.Message}");
                    continue;
                }

                foreach (var tag in ParseScene(text, scriptGuids))
                    Add(index, new TagUsage
                    {
                        Tag = tag,
                        Kind = UsageKind.ClosedScene,
                        AssetPath = path
                    });
            }

            return true;
        }

        private static void CollectFrom(GameObject root, UsageKind kind, string assetPath,
            IDictionary<string, List<TagUsage>> index, bool keepReference)
        {
            foreach (var gate in root.GetComponentsInChildren<ContentTagGateHandler>(true))
                AddComponent(gate.gameObject, gate.EditorTags, nameof(ContentTagGateHandler), kind, assetPath,
                    index, keepReference);

            foreach (var handler in root.GetComponentsInChildren<ContentTagObjectHandler>(true))
                AddComponent(handler.gameObject, handler.EditorTags, nameof(ContentTagObjectHandler), kind,
                    assetPath, index, keepReference);
        }

        private static void AddComponent(GameObject owner, IReadOnlyList<string> tags, string component,
            UsageKind kind, string assetPath, IDictionary<string, List<TagUsage>> index, bool keepReference)
        {
            if (tags == null)
                return;

            foreach (var tag in tags)
            {
                if (string.IsNullOrEmpty(tag))
                    continue;

                Add(index, new TagUsage
                {
                    Tag = tag,
                    Kind = kind,
                    AssetPath = assetPath,
                    ObjectPath = HierarchyPath(owner.transform),
                    Component = component,
                    SceneObject = keepReference ? owner : null
                });
            }
        }

        private static void Add(IDictionary<string, List<TagUsage>> index, TagUsage usage)
        {
            if (!index.TryGetValue(usage.Tag, out var list))
            {
                list = new List<TagUsage>();
                index[usage.Tag] = list;
            }

            list.Add(usage);
        }

        /// <summary>Путь объекта от корня иерархии, без самого корня: им адресуется <c>Transform.Find</c>.</summary>
        internal static string HierarchyPath(Transform target)
        {
            var path = target.name;
            var parent = target.parent;
            while (parent != null)
            {
                path = $"{parent.name}/{path}";
                parent = parent.parent;
            }

            return path;
        }

        /// <summary>GUID скриптов-носителей: по ним документ сцены опознаётся как наш компонент.</summary>
        private static HashSet<string> ScriptGuids()
        {
            var result = new HashSet<string>();
            foreach (var guid in AssetDatabase.FindAssets("t:MonoScript ContentTag"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var script = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
                var type = script == null ? null : script.GetClass();
                if (type == typeof(ContentTagGateHandler) || type == typeof(ContentTagObjectHandler))
                    result.Add(guid);
            }

            return result;
        }

        /// <summary>
        /// Теги, перечисленные в документах наших компонентов. Разбор ограничен телом документа: поиск
        /// имени тега по всему файлу давал бы ложные срабатывания на посторонних полях.
        /// </summary>
        internal static IEnumerable<string> ParseScene(string text, ICollection<string> scriptGuids)
        {
            foreach (var document in text.Split(new[] { "--- !u!" }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (!HasScript(document, scriptGuids))
                    continue;

                var inTags = false;
                foreach (var raw in document.Split('\n'))
                {
                    var line = raw.TrimEnd('\r').Trim();
                    if (!inTags)
                    {
                        inTags = line == "tags:";
                        continue;
                    }

                    if (!line.StartsWith("- "))
                        break;

                    var tag = line.Substring(2).Trim().Trim('"');
                    if (!string.IsNullOrEmpty(tag))
                        yield return tag;
                }
            }
        }

        private static bool HasScript(string document, ICollection<string> scriptGuids)
        {
            var marker = document.IndexOf("m_Script:", StringComparison.Ordinal);
            if (marker < 0)
                return false;

            var line = document.Substring(marker,
                Math.Min(200, document.Length - marker));

            foreach (var guid in scriptGuids)
                if (line.Contains(guid))
                    return true;

            return false;
        }
    }
}
#endif
