using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEditor;

namespace Vortex.Unity.AssetSwapSystem
{
    /// <summary>
    /// Сканер runtime-ассетов на предмет прямых ссылок на variant-файлы. Обнаруживает
    /// «протаскивание в билд» — единственный способ, которым variant может утечь мимо
    /// правила <c>Editor/</c>-исключения.
    ///
    /// Асинхронный: <c>GetDependencies(recursive:true)</c> на 500+ prefab/scene/SO —
    /// секунды-десятки секунд wall-time. Батчим по <see cref="YieldBatchSize"/>
    /// кандидатов и <c>await UniTask.Yield()</c> между батчами: main-thread остаётся
    /// отзывчивым, DisplayProgressBar перерисовывается, Debug.Log видны в реальном времени.
    /// Cancellation через <see cref="CancellationToken"/> — тихий возврат частичного
    /// результата, без исключения (проверка результата — на стороне вызывающего).
    /// </summary>
    internal static class AssetSwapRuntimeHolderScanner
    {
        // Типы, где реальный референс на ассет может быть сериализован. Editor-only ассеты
        // и .cs-скрипты не проверяем — Unity в билд их не включит.
        private const string RuntimeAssetFilter =
            "t:Prefab t:Scene t:ScriptableObject t:Material t:AnimatorController t:AnimationClip";

        // Cadence yield'а по кандидатам. 20 — компромисс между «плавным UI» и «не тратить
        // время на переключения контекста»; тот же порядок, что в LocalizationDriverExtLoading.
        private const int YieldBatchSize = 20;

        /// <summary>
        /// Все пары <c>(variant-путь, holder-путь)</c>, где runtime-ассет (prefab/scene/
        /// SO/material/controller/anim) ссылается на любой из variant-ассетов.
        /// Runtime-ассетами считаются файлы ВНЕ путей, содержащих <c>/Editor/</c>.
        ///
        /// <paramref name="onProgress"/> вызывается на каждом batch-yield с долей
        /// просканированных кандидатов (0..1). <paramref name="ct"/> при отмене приводит
        /// к тихому возврату уже накопленных результатов; вызывающий должен трактовать
        /// это как «валидация неполная» (см. <c>AssetSwapController.ValidateAsync</c>).
        /// </summary>
        internal static async UniTask<List<(string variantPath, string holderPath)>> FindRuntimeHoldersAsync(
            IReadOnlyCollection<string> variantGuids,
            Action<float> onProgress = null,
            CancellationToken ct = default)
        {
            var result = new List<(string variantPath, string holderPath)>();
            if (variantGuids == null || variantGuids.Count == 0) return result;

            var variantGuidSet = new HashSet<string>(variantGuids);
            var candidates = AssetDatabase.FindAssets(RuntimeAssetFilter);
            var total = candidates.Length;

            for (var i = 0; i < total; i++)
            {
                if (ct.IsCancellationRequested) return result;

                var holderPath = AssetDatabase.GUIDToAssetPath(candidates[i]);
                if (!string.IsNullOrEmpty(holderPath) && !IsUnderEditorFolder(holderPath))
                {
                    var deps = AssetDatabase.GetDependencies(holderPath, recursive: true);
                    foreach (var dep in deps)
                    {
                        if (dep == holderPath) continue;
                        var depGuid = AssetDatabase.AssetPathToGUID(dep);
                        if (string.IsNullOrEmpty(depGuid)) continue;
                        if (variantGuidSet.Contains(depGuid))
                            result.Add((dep, holderPath));
                    }
                }

                if (i % YieldBatchSize == 0)
                {
                    onProgress?.Invoke((i + 1) / (float)total);
                    await UniTask.Yield();
                }
            }

            onProgress?.Invoke(1f);
            return result;
        }

        private static bool IsUnderEditorFolder(string path)
        {
            // Как Unity считает Editor-папки: имя папки Editor в любом месте пути.
            return path.Contains("/Editor/") || path.EndsWith("/Editor");
        }
    }
}
