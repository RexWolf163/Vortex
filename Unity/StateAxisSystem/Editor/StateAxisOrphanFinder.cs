using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Vortex.Unity.StateAxisSystem.Presets;

namespace Vortex.Unity.StateAxisSystem.EditorTools
{
    /// <summary>
    /// Окно поиска "беспризорных" сгенерированных <c>.cs</c>: файлов, содержащих
    /// <c>: StateAxis</c>, лежащих в папках вместе с пресетами, но не упомянутых
    /// в <see cref="StateAxisPreset.LastGeneratedPath"/> ни одного пресета.
    ///
    /// Меню: <c>Tools/Vortex/StateAxis/Find orphans</c>.
    /// </summary>
    public class StateAxisOrphanFinder : EditorWindow
    {
        private readonly List<string> _orphans = new();
        private readonly HashSet<string> _selected = new();
        private Vector2 _scroll;

        [MenuItem("Tools/Vortex/StateAxis/Find orphans")]
        private static void Open()
        {
            var w = GetWindow<StateAxisOrphanFinder>("StateAxis orphans");
            w.minSize = new Vector2(420, 300);
            w.Scan();
        }

        private void OnGUI()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Rescan", GUILayout.Width(80)))
                    Scan();
                EditorGUILayout.LabelField($"Найдено: {_orphans.Count}", GUILayout.Width(120));
                GUILayout.FlexibleSpace();
                using (new EditorGUI.DisabledScope(_selected.Count == 0))
                {
                    if (GUILayout.Button($"Delete selected ({_selected.Count})", GUILayout.Width(180)))
                        DeleteSelected();
                }
            }

            EditorGUILayout.Space();

            if (_orphans.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    "Беспризорных файлов не найдено.\n\n" +
                    "Поиск идёт по папкам, где лежат StateAxisPreset-ассеты. " +
                    "Файл считается беспризорным, если содержит ': StateAxis' и не " +
                    "упомянут в lastGeneratedPath ни одного пресета.",
                    MessageType.Info);
                return;
            }

            using (var sv = new EditorGUILayout.ScrollViewScope(_scroll))
            {
                _scroll = sv.scrollPosition;
                foreach (var path in _orphans)
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        var was = _selected.Contains(path);
                        var now = EditorGUILayout.Toggle(was, GUILayout.Width(18));
                        if (now != was)
                        {
                            if (now) _selected.Add(path); else _selected.Remove(path);
                        }
                        EditorGUILayout.LabelField(path);
                        if (GUILayout.Button("Ping", GUILayout.Width(50)))
                        {
                            var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
                            if (asset != null) EditorGUIUtility.PingObject(asset);
                        }
                    }
                }
            }
        }

        private void Scan()
        {
            _orphans.Clear();
            _selected.Clear();

            var claimed = new HashSet<string>();
            var presetGuids = AssetDatabase.FindAssets($"t:{nameof(StateAxisPreset)}");
            var folders = new HashSet<string>();

            foreach (var g in presetGuids)
            {
                var path = AssetDatabase.GUIDToAssetPath(g);
                var preset = AssetDatabase.LoadAssetAtPath<StateAxisPreset>(path);
                if (preset == null) continue;

                if (!string.IsNullOrEmpty(preset.LastGeneratedPath))
                    claimed.Add(preset.LastGeneratedPath);

                var dir = Path.GetDirectoryName(path)?.Replace('\\', '/');
                if (!string.IsNullOrEmpty(dir))
                    folders.Add(dir);
            }

            foreach (var folder in folders)
            {
                if (!Directory.Exists(folder)) continue;
                foreach (var file in Directory.EnumerateFiles(folder, "*.cs", SearchOption.TopDirectoryOnly))
                {
                    var unity = file.Replace('\\', '/');
                    if (claimed.Contains(unity)) continue;

                    string text;
                    try { text = File.ReadAllText(unity); }
                    catch { continue; }

                    if (text.Contains(": StateAxis"))
                        _orphans.Add(unity);
                }
            }

            _orphans.Sort();
            Repaint();
        }

        private void DeleteSelected()
        {
            if (!EditorUtility.DisplayDialog("StateAxis",
                $"Удалить {_selected.Count} файл(ов)?\n\n" + string.Join("\n", _selected.Take(10)) +
                (_selected.Count > 10 ? $"\n… и ещё {_selected.Count - 10}" : ""),
                "Удалить", "Отмена"))
                return;

            foreach (var path in _selected.ToList())
                AssetDatabase.DeleteAsset(path);

            AssetDatabase.Refresh();
            Scan();
        }
    }
}
