using System.Collections.Generic;
using UnityEditor;

namespace Vortex.Unity.AssetSwapSystem
{
    /// <summary>
    /// Сканер runtime-ассетов на предмет прямых ссылок на variant-файлы. Обнаруживает
    /// «протаскивание в билд» — единственный способ, которым variant может утечь мимо
    /// правила <c>Editor/</c>-исключения (см. диалог §7 п.9 / п.6 предыдущей итерации).
    /// </summary>
    internal static class AssetSwapRuntimeHolderScanner
    {
        // Типы, где реальный референс на ассет может быть сериализован. Editor-only ассеты
        // и .cs-скрипты не проверяем — Unity в билд их не включит.
        private const string RuntimeAssetFilter =
            "t:Prefab t:Scene t:ScriptableObject t:Material t:AnimatorController t:AnimationClip";

        /// <summary>
        /// Все пары <c>(variant-путь, holder-путь)</c>, где runtime-ассет (prefab/scene/
        /// SO/material/controller/anim) ссылается на любой из variant-ассетов.
        /// Runtime-ассетами считаются файлы ВНЕ путей, содержащих <c>/Editor/</c>.
        /// </summary>
        internal static IEnumerable<(string variantPath, string holderPath)> FindRuntimeHolders(
            IReadOnlyCollection<string> variantGuids)
        {
            if (variantGuids == null || variantGuids.Count == 0) yield break;

            var variantGuidSet = new HashSet<string>(variantGuids);
            var candidates = AssetDatabase.FindAssets(RuntimeAssetFilter);

            foreach (var candidateGuid in candidates)
            {
                var holderPath = AssetDatabase.GUIDToAssetPath(candidateGuid);
                if (string.IsNullOrEmpty(holderPath)) continue;
                if (IsUnderEditorFolder(holderPath)) continue;

                var deps = AssetDatabase.GetDependencies(holderPath, recursive: true);
                foreach (var dep in deps)
                {
                    if (dep == holderPath) continue;
                    var depGuid = AssetDatabase.AssetPathToGUID(dep);
                    if (string.IsNullOrEmpty(depGuid)) continue;
                    if (variantGuidSet.Contains(depGuid))
                        yield return (dep, holderPath);
                }
            }
        }

        private static bool IsUnderEditorFolder(string path)
        {
            // Как Unity считает Editor-папки: имя папки Editor в любом месте пути.
            return path.Contains("/Editor/") || path.EndsWith("/Editor");
        }
    }
}
