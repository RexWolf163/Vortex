#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Vortex.Sdk.ContentTagsSystem.Bus;
using Vortex.Sdk.ContentTagsSystem.Model;

namespace Vortex.Sdk.ContentTagsSystem.Editor
{
    /// <summary>
    /// Ревизия разметки перед сборкой издания: что включено в активном бандле и кто носит каждый тег.
    /// Окно строго на чтение — активное издание меняется в инспекторе ассета настроек, чтобы правка
    /// конфигурации сборки не делалась мимоходом из инструмента просмотра.
    /// </summary>
    internal sealed class ContentTagsWindow : EditorWindow
    {
        private Dictionary<string, List<TagUsage>> _index;

        private string[] _declared = new string[0];

        private int _selected;

        private Vector2 _scroll;

        [MenuItem("Tools/Vortex/Content Tags")]
        private static void Open() => GetWindow<ContentTagsWindow>("Content Tags").Show();

        private void OnEnable() => _declared = ContentTagsCatalog.EditorKeys().ToArray();

        private void OnGUI()
        {
            DrawToolbar();
            DrawActiveBundle();

            if (_index == null)
            {
                EditorGUILayout.HelpBox("Индекс не собран. Нажмите «Обновить».", MessageType.Info);
                return;
            }

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            DrawSelectedTag();
            DrawUnused();
            DrawUndeclared();
            EditorGUILayout.EndScrollView();
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            if (GUILayout.Button("Обновить", EditorStyles.toolbarButton, GUILayout.Width(90)))
                Rebuild();
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            if (!ContentTagsIndex.ScenesReadable)
                EditorGUILayout.HelpBox("Asset Serialization не Force Text — закрытые сцены не сканируются, " +
                                        "результат неполный.", MessageType.Warning);
        }

        private void DrawActiveBundle()
        {
            var bundle = ContentBus.ActiveBundle;
            var active = ContentBus.ActiveTags;

            EditorGUILayout.LabelField("Активное издание",
                string.IsNullOrEmpty(bundle) ? "не задано — всё размеченное выключено" : bundle,
                EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Включено тегов", active.Count.ToString());

            if (active.Count > 0)
                EditorGUILayout.LabelField(string.Empty, string.Join(", ", active));

            EditorGUILayout.Space();
        }

        private void DrawSelectedTag()
        {
            if (_declared.Length == 0)
            {
                EditorGUILayout.HelpBox("В ассете настроек нет объявленных тегов.", MessageType.Info);
                return;
            }

            _selected = EditorGUILayout.Popup("Тег", Mathf.Clamp(_selected, 0, _declared.Length - 1), _declared);
            var tag = _declared[_selected];

            var isActive = ContentBus.ActiveTags.Contains(tag);
            EditorGUILayout.LabelField("Состояние", isActive ? "включён" : "выключен");

            if (!_index.TryGetValue(tag, out var usages))
            {
                EditorGUILayout.HelpBox("Носителей не найдено.", MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField($"Носители ({usages.Count})", EditorStyles.boldLabel);
            foreach (var usage in usages)
                DrawUsage(usage);
        }

        private static void DrawUsage(TagUsage usage)
        {
            EditorGUILayout.BeginHorizontal();

            var label = usage.Kind switch
            {
                UsageKind.Prefab => $"Префаб · {usage.ObjectPath} · {usage.Component}",
                UsageKind.OpenScene => $"Сцена · {usage.ObjectPath} · {usage.Component}",
                _ => "Сцена · носитель не определён"
            };

            EditorGUILayout.LabelField(new GUIContent(label, usage.AssetPath));

            if (GUILayout.Button("Find", GUILayout.Width(50)))
                Find(usage);

            EditorGUILayout.EndHorizontal();
        }

        private void DrawUnused()
        {
            var unused = _declared.Where(tag => !_index.ContainsKey(tag)).ToArray();
            if (unused.Length == 0)
                return;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField($"Объявлены, но не используются ({unused.Length})",
                EditorStyles.boldLabel);
            EditorGUILayout.LabelField(string.Empty, string.Join(", ", unused));
        }

        private void DrawUndeclared()
        {
            var declared = new HashSet<string>(_declared);
            var undeclared = _index.Keys.Where(tag => !declared.Contains(tag)).ToArray();
            if (undeclared.Length == 0)
                return;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField($"Используются, но не объявлены ({undeclared.Length})",
                EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Эти теги не включит ни одно издание: опечатка в компоненте или " +
                                    "удалённое объявление.", MessageType.Warning);

            foreach (var tag in undeclared)
            foreach (var usage in _index[tag])
            {
                EditorGUILayout.LabelField(tag, EditorStyles.miniBoldLabel);
                DrawUsage(usage);
            }
        }

        private void Rebuild()
        {
            ContentBus.EditorRebuild();
            _declared = ContentTagsCatalog.EditorKeys().ToArray();

            var index = ContentTagsIndex.Build();
            if (index != null)
                _index = index;
        }

        private static void Find(TagUsage usage)
        {
            switch (usage.Kind)
            {
                case UsageKind.OpenScene:
                    if (usage.SceneObject == null)
                    {
                        Debug.LogWarning($"[ContentTags] Объект недоступен: сцена «{usage.AssetPath}» закрыта. " +
                                         "Соберите индекс заново.");
                        return;
                    }

                    Selection.activeGameObject = usage.SceneObject;
                    EditorGUIUtility.PingObject(usage.SceneObject);
                    return;

                case UsageKind.Prefab:
                    OpenPrefabAt(usage);
                    return;

                default:
                    var scene = AssetDatabase.LoadAssetAtPath<SceneAsset>(usage.AssetPath);
                    if (scene != null)
                        EditorGUIUtility.PingObject(scene);
                    return;
            }
        }

        // Инструмент ревизии обязан доводить до носителя: в крупном префабе искать его руками дороже,
        // чем открыть стадию редактирования и выделить объект.
        private static void OpenPrefabAt(TagUsage usage)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(usage.AssetPath);
            if (prefab == null)
            {
                Debug.LogWarning($"[ContentTags] Префаб «{usage.AssetPath}» не найден. Соберите индекс заново.");
                return;
            }

            AssetDatabase.OpenAsset(prefab);

            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage == null)
                return;

            var target = Resolve(stage.prefabContentsRoot.transform, usage.ObjectPath);
            if (target == null)
                return;

            Selection.activeGameObject = target.gameObject;
            EditorGUIUtility.PingObject(target.gameObject);
        }

        /// <summary>Объект по пути вида «Root/Child/Sub»: первый сегмент — сам корень стадии.</summary>
        private static Transform Resolve(Transform root, string path)
        {
            if (string.IsNullOrEmpty(path))
                return root;

            var separator = path.IndexOf('/');
            return separator < 0 ? root : root.Find(path.Substring(separator + 1));
        }
    }
}
#endif
