#if UNITY_EDITOR
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using Vortex.Unity.MappedParametersSystem.Base.Preset;

namespace Vortex.Unity.MappedParametersSystem.Editor
{
    public static class MappedParameterGraphExporter
    {
        [MenuItem("Vortex/Debug/Export Mapped Parameters into Graph")]
        public static void ExportGraph()
        {
            var maps = Resources.LoadAll<ParametersMapStorage>("");
            if (maps.Length == 0)
            {
                Debug.LogWarning("No ParametersMap found in Resources.");
                return;
            }

            foreach (var map in maps)
            {
                if (map == null) continue;
                var graph = BuildGraph(map);
                var path = EditorUtility.SaveFilePanel(
                    "Save Parameter Graph",
                    Application.dataPath,
                    $"{map.name}_graph.dot",
                    "dot"
                );

                if (!string.IsNullOrEmpty(path))
                {
                    File.WriteAllText(path, graph, Encoding.UTF8);
                    Debug.Log($"Graph exported to: {path}");
                }
            }
        }

        private static string BuildGraph(ParametersMapStorage map)
        {
            var sb = new StringBuilder();
            sb.AppendLine("digraph Parameters {");
            sb.AppendLine("  rankdir=TB;");
            sb.AppendLine("  node [shape=box, style=filled, fillcolor=\"#e0f7fa\"];");

            // Базовые параметры — выделяем цветом
            foreach (var baseParam in map.baseParams)
            {
                if (string.IsNullOrWhiteSpace(baseParam)) continue;
                sb.AppendLine($"  \"{Escape(baseParam)}\" [fillcolor=\"#b3e5fc\"];");
            }

            // Производные параметры и связи
            foreach (var derived in map.mappedParams)
            {
                if (derived == null || string.IsNullOrWhiteSpace(derived.Name)) continue;
                sb.AppendLine($"  \"{Escape(derived.Name)}\";");

                foreach (var link in derived.Parents)
                {
                    if (string.IsNullOrWhiteSpace(link.Parent)) continue;
                    sb.AppendLine(
                        $"  \"{Escape(link.Parent)}\" -> \"{Escape(derived.Name)}\" [label=\"{link.Cost}\"];");
                }
            }

            sb.AppendLine("}");
            return sb.ToString();
        }

        private static string Escape(string s) => s.Replace("\"", "\\\"");
    }
}
#endif