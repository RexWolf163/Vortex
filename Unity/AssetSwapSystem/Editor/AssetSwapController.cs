using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace Vortex.Unity.AssetSwapSystem
{
    /// <summary>
    /// Public API AssetSwapSystem. Точки входа для страницы ProjectSettings, для окна
    /// Match Window и для CI-batch'ей.
    ///
    /// <b>Sync и async варианты.</b> Тяжёлые операции (<see cref="ValidateAsync"/>,
    /// <see cref="ValidateAllAsync"/>, <see cref="ApplyAsync"/>) — асинхронные с
    /// <c>await UniTask.Yield()</c> между шагами: UI редактора не подвешивается,
    /// <c>Debug.Log</c> и <c>DisplayCancelableProgressBar</c> обновляются в реальном
    /// времени. Sync-обёртки (<see cref="Validate"/>, <see cref="ValidateAll"/>,
    /// <see cref="Apply"/>) блокируют через <c>GetAwaiter().GetResult()</c> — оставлены
    /// для CI (<c>unity -batchmode -executeMethod</c>), где UI-freeze не имеет значения
    /// и внешний вызов не понимает <c>UniTask</c>.
    ///
    /// Все ошибки — <c>Debug.LogError</c> с префиксом <c>[AssetSwap]</c> и указанием
    /// группы/пути; никакого silent-fallback'а.
    /// </summary>
    public static class AssetSwapController
    {
        private const string LogPrefix = "[AssetSwap]";

        // Cadence yield'а в Apply/CheckUniqueNames-like циклах. Тот же порядок, что в
        // холдер-сканере и Localization-драйвере (см. LocalizationDriverExtLoading.RunAsync).
        private const int YieldBatchSize = 20;

        // Веса шагов Validate в общем прогресс-баре (в сумме 1.0). Holders — самый долгий
        // (GetDependencies по всем runtime-ассетам), ему львиная доля.
        private const float StepWeightUnique   = 0.05f;
        private const float StepWeightPairs    = 0.10f;
        private const float StepWeightReverse  = 0.05f;
        private const float StepWeightTypes    = 0.10f;
        private const float StepWeightHolders  = 0.70f;

        /// <summary>Зарегистрировать новую группу. Тонкая операция, sync.</summary>
        public static int NewGroup()
        {
            var settings = AssetSwapSettings.instance;
            settings.Sanitize();

            var n = settings.GetFreeIndex();
            settings.AddGroup(new AssetSwapGroup { index = n });
            settings.SaveToDisk();

            if (Selection.assetGUIDs is { Length: > 0 })
                AssetSwapLabelHelper.AddLabelToSelected(AssetSwapLabelHelper.TargetLabel(n));

            return n;
        }

        /// <summary>Добавить новый вариант в группу N. Тонкая операция, sync.</summary>
        public static int AddVariant(int groupIndex)
        {
            var settings = AssetSwapSettings.instance;
            settings.Sanitize();

            var group = settings.GetGroup(groupIndex);
            if (group == null)
            {
                Debug.LogError($"{LogPrefix} AddVariant: group AssetGroup{groupIndex} not found.");
                return -1;
            }

            var k = group.variantsCount + 1;
            group.variantsCount = k;
            group.variantComments ??= new List<string>();
            while (group.variantComments.Count < k)
                group.variantComments.Add(string.Empty);
            settings.SaveToDisk();

            if (Selection.assetGUIDs is { Length: > 0 })
                AssetSwapLabelHelper.AddLabelToSelected(AssetSwapLabelHelper.VariantLabel(groupIndex, k));

            return k;
        }

        /// <summary>Удалить группу N (batch SaveAssets). Тонкая операция, sync.</summary>
        public static void RemoveGroup(int groupIndex)
        {
            var settings = AssetSwapSettings.instance;
            var group = settings.GetGroup(groupIndex);
            if (group == null)
            {
                Debug.LogError($"{LogPrefix} RemoveGroup: group AssetGroup{groupIndex} not found.");
                return;
            }

            var labels = new string[group.variantsCount + 1];
            labels[0] = AssetSwapLabelHelper.TargetLabel(groupIndex);
            for (var k = 1; k <= group.variantsCount; k++)
                labels[k] = AssetSwapLabelHelper.VariantLabel(groupIndex, k);
            AssetSwapLabelHelper.RemoveLabelsFromAll(labels);

            settings.RemoveGroup(groupIndex);
            settings.SaveToDisk();
        }

        /// <summary>Sync-обёртка над <see cref="ValidateAsync"/> для CI-хэндлеров.</summary>
        public static bool Validate(int groupIndex) =>
            ValidateAsync(groupIndex, showProgressBar: false).GetAwaiter().GetResult();

        /// <summary>Sync-обёртка над <see cref="ValidateAllAsync"/> для CI-хэндлеров.</summary>
        public static bool ValidateAll() =>
            ValidateAllAsync(showProgressBar: false).GetAwaiter().GetResult();

        /// <summary>Sync-обёртка над <see cref="ApplyAsync"/> для CI-хэндлеров.</summary>
        public static void Apply(int groupIndex, int variantIndex) =>
            ApplyAsync(groupIndex, variantIndex, showProgressBar: false).GetAwaiter().GetResult();

        /// <summary>
        /// Асинхронная валидация группы: 5 шагов с промежуточным <c>Debug.Log</c> и
        /// <c>await UniTask.Yield()</c> между ними. При <paramref name="showProgressBar"/>=true
        /// показывается <c>DisplayCancelableProgressBar</c>; отмена пользователем прерывает
        /// прогон и возвращает <c>false</c>.
        ///
        /// <paramref name="onCount"/> — опциональный колбэк, куда «пишутся» посчитанные
        /// размеры label-множеств: сразу после <c>FindAssetsByLabel</c> вызывается пара
        /// <c>(TargetLabel, targets.Length)</c> + по одному вызову на каждый variant.
        /// UI-обёртка использует его для инкрементального обновления счётчиков на странице
        /// без пост-фактум <c>Clear+пересчёт всех групп</c> (см. SettingsProvider).
        ///
        /// Возвращает <c>true</c> только если все шаги завершились без ошибок и пользователь
        /// не отменил. Отменённая валидация трактуется как «не подтвердила чистоту» — Apply
        /// её не пропустит.
        /// </summary>
        public static async UniTask<bool> ValidateAsync(
            int groupIndex,
            bool showProgressBar,
            Action<string, int> onCount = null)
        {
            var settings = AssetSwapSettings.instance;
            var group = settings.GetGroup(groupIndex);
            if (group == null)
            {
                Debug.LogError($"{LogPrefix} Validate: group AssetGroup{groupIndex} not found.");
                return false;
            }

            using var cts = new CancellationTokenSource();
            var title = $"{LogPrefix} AssetGroup{groupIndex}";

            bool Report(string info, float progress)
            {
                if (!showProgressBar) return false;
                if (EditorUtility.DisplayCancelableProgressBar(title, info, progress))
                {
                    cts.Cancel();
                    return true;
                }
                return false;
            }

            try
            {
                return await ValidateInternalAsync(group, Report, cts.Token, baseProgress: 0f, span: 1f, onCount);
            }
            finally
            {
                if (showProgressBar) EditorUtility.ClearProgressBar();
            }
        }

        /// <summary>
        /// Асинхронная валидация всех групп по очереди. Прогресс-бар масштабируется
        /// пропорционально числу групп; отмена прерывает цикл, возвращает <c>false</c>.
        /// <paramref name="onCount"/> — см. <see cref="ValidateAsync"/>.
        /// </summary>
        public static async UniTask<bool> ValidateAllAsync(
            bool showProgressBar,
            Action<string, int> onCount = null)
        {
            var settings = AssetSwapSettings.instance;
            settings.Sanitize();

            var groups = settings.Groups;
            if (groups.Count == 0) return true;

            using var cts = new CancellationTokenSource();
            const string title = LogPrefix + " Validate All";

            bool Report(string info, float progress)
            {
                if (!showProgressBar) return false;
                if (EditorUtility.DisplayCancelableProgressBar(title, info, progress))
                {
                    cts.Cancel();
                    return true;
                }
                return false;
            }

            var ok = true;
            try
            {
                var span = 1f / groups.Count;
                for (var i = 0; i < groups.Count; i++)
                {
                    if (cts.IsCancellationRequested) return false;
                    var group = groups[i];
                    Debug.Log($"{LogPrefix} Validating AssetGroup{group.index}...");
                    var groupOk = await ValidateInternalAsync(
                        group, Report, cts.Token,
                        baseProgress: i * span, span: span, onCount);
                    ok &= groupOk;
                }
            }
            finally
            {
                if (showProgressBar) EditorUtility.ClearProgressBar();
            }
            return ok;
        }

        /// <summary>
        /// Асинхронный Apply: сперва <see cref="ValidateInternalAsync"/> занимает первую
        /// половину прогресс-бара, затем File.Copy-loop — вторую. Между копиями —
        /// <c>await UniTask.Yield()</c> каждые <see cref="YieldBatchSize"/> файлов.
        /// <c>StartAssetEditing/StopAssetEditing</c> обеспечивает batch-reimport.
        /// <paramref name="onCount"/> — см. <see cref="ValidateAsync"/>: сработает в фазе
        /// Validate, покроет счётчики этой группы даже если пользователь отменит на этапе
        /// копирования.
        /// </summary>
        public static async UniTask ApplyAsync(
            int groupIndex,
            int variantIndex,
            bool showProgressBar,
            Action<string, int> onCount = null)
        {
            var settings = AssetSwapSettings.instance;
            var group = settings.GetGroup(groupIndex);
            if (group == null)
            {
                Debug.LogError($"{LogPrefix} Apply: group AssetGroup{groupIndex} not found.");
                return;
            }
            if (variantIndex < 1 || variantIndex > group.variantsCount)
            {
                Debug.LogError(
                    $"{LogPrefix} Apply: variant K={variantIndex} out of range [1..{group.variantsCount}] " +
                    $"for AssetGroup{groupIndex}.");
                return;
            }

            using var cts = new CancellationTokenSource();
            var title = $"{LogPrefix} Apply AssetGroup{groupIndex} → Variant{variantIndex}";

            bool Report(string info, float progress)
            {
                if (!showProgressBar) return false;
                if (EditorUtility.DisplayCancelableProgressBar(title, info, progress))
                {
                    cts.Cancel();
                    return true;
                }
                return false;
            }

            try
            {
                var validated = await ValidateInternalAsync(
                    group, Report, cts.Token,
                    baseProgress: 0f, span: 0.5f, onCount);
                if (!validated)
                {
                    Debug.LogError($"{LogPrefix} Apply blocked for AssetGroup{groupIndex}: fix validation errors first.");
                    return;
                }
                if (cts.IsCancellationRequested) return;

                var targets = AssetSwapLabelHelper.FindAssetsByLabel(AssetSwapLabelHelper.TargetLabel(groupIndex));
                var variants = AssetSwapLabelHelper.FindAssetsByLabel(AssetSwapLabelHelper.VariantLabel(groupIndex, variantIndex));

                var applied = 0;
                AssetDatabase.StartAssetEditing();
                try
                {
                    for (var i = 0; i < targets.Length; i++)
                    {
                        if (cts.IsCancellationRequested) break;

                        var targetPath = targets[i];
                        var name = Path.GetFileName(targetPath);
                        var variantPath = variants.FirstOrDefault(v => Path.GetFileName(v) == name);
                        if (variantPath == null) continue; // не должно случиться после Validate

                        var progressFraction = 0.5f + 0.5f * (i / (float)targets.Length);
                        if (Report($"Copying {name} ({i + 1}/{targets.Length})...", progressFraction))
                            break;

                        try
                        {
                            File.Copy(variantPath, targetPath, overwrite: true);
                            AssetDatabase.ImportAsset(targetPath);
                            applied++;
                        }
                        catch (Exception e)
                        {
                            Debug.LogError(
                                $"{LogPrefix} File.Copy failed: '{variantPath}' → '{targetPath}': {e.Message}. " +
                                "State may be partial — restore via git.");
                        }

                        if (i % YieldBatchSize == 0)
                            await UniTask.Yield();
                    }
                }
                finally
                {
                    AssetDatabase.StopAssetEditing();
                }

                Debug.Log($"{LogPrefix} Applied AssetGroup{groupIndex} Variant{variantIndex} to {applied}/{targets.Length} assets.");
            }
            finally
            {
                if (showProgressBar) EditorUtility.ClearProgressBar();
            }
        }

        /// <summary>
        /// Общая реализация валидации, используется <see cref="ValidateAsync"/>,
        /// <see cref="ValidateAllAsync"/> и первой фазой <see cref="ApplyAsync"/>.
        /// <paramref name="baseProgress"/> + <paramref name="span"/> позволяют вложить
        /// прогон в родительский progress-bar (например, ValidateAll делит 0..1 на N групп).
        /// <paramref name="onCount"/> отдаёт наверх посчитанные размеры label-множеств
        /// сразу после <c>FindAssetsByLabel</c> — до тяжёлых шагов, чтобы UI успел
        /// подхватить счётчики даже если пользователь Cancel'нёт валидацию рано.
        /// </summary>
        private static async UniTask<bool> ValidateInternalAsync(
            AssetSwapGroup group,
            Func<string, float, bool> report,
            CancellationToken ct,
            float baseProgress,
            float span,
            Action<string, int> onCount)
        {
            var ok = true;
            var groupIndex = group.index;
            float acc = 0f;

            float Scale(float local) => baseProgress + local * span;
            bool Cancelled() => ct.IsCancellationRequested;

            if (report($"AssetGroup{groupIndex}: collecting labels...", Scale(acc))) return false;

            var targets = AssetSwapLabelHelper.FindAssetsByLabel(AssetSwapLabelHelper.TargetLabel(groupIndex));
            var variantsByK = new Dictionary<int, string[]>();
            for (var k = 1; k <= group.variantsCount; k++)
                variantsByK[k] = AssetSwapLabelHelper.FindAssetsByLabel(AssetSwapLabelHelper.VariantLabel(groupIndex, k));

            // Опубликовать счётчики РАНЬШЕ тяжёлых шагов: даже отменённая на holders'ах
            // валидация оставит на UI-странице актуальные цифры Targets/VariantK.
            onCount?.Invoke(AssetSwapLabelHelper.TargetLabel(groupIndex), targets.Length);
            foreach (var kv in variantsByK)
                onCount?.Invoke(AssetSwapLabelHelper.VariantLabel(groupIndex, kv.Key), kv.Value.Length);

            Debug.Log($"{LogPrefix} AssetGroup{groupIndex}: {targets.Length} target(s), {group.variantsCount} variant(s).");
            await UniTask.Yield();
            if (Cancelled()) return false;

            // 1) Unique names.
            if (report($"AssetGroup{groupIndex}: checking unique names...", Scale(acc))) return false;
            ok &= CheckUniqueNames(targets, $"AssetGroup{groupIndex} targets");
            foreach (var kv in variantsByK)
                ok &= CheckUniqueNames(kv.Value, $"AssetGroup{groupIndex}_Variant{kv.Key}");
            acc += StepWeightUnique;
            await UniTask.Yield();
            if (Cancelled()) return false;

            // 2) Pair completeness.
            if (report($"AssetGroup{groupIndex}: checking pair completeness...", Scale(acc))) return false;
            for (var i = 0; i < targets.Length; i++)
            {
                if (Cancelled()) return false;
                var name = Path.GetFileName(targets[i]);
                for (var k = 1; k <= group.variantsCount; k++)
                {
                    if (!variantsByK[k].Any(v => Path.GetFileName(v) == name))
                    {
                        Debug.LogError($"{LogPrefix} Target '{targets[i]}' has no pair in AssetGroup{groupIndex}_Variant{k}.");
                        ok = false;
                    }
                }
                if (i % YieldBatchSize == 0) await UniTask.Yield();
            }
            acc += StepWeightPairs;

            // 3) Reverse completeness.
            if (report($"AssetGroup{groupIndex}: checking reverse completeness...", Scale(acc))) return false;
            foreach (var kv in variantsByK)
            {
                if (Cancelled()) return false;
                for (var i = 0; i < kv.Value.Length; i++)
                {
                    var name = Path.GetFileName(kv.Value[i]);
                    if (!targets.Any(t => Path.GetFileName(t) == name))
                    {
                        Debug.LogError($"{LogPrefix} Variant '{kv.Value[i]}' has no target in AssetGroup{groupIndex}.");
                        ok = false;
                    }
                    if (i % YieldBatchSize == 0) await UniTask.Yield();
                }
            }
            acc += StepWeightReverse;

            // 4) Importer type match.
            if (report($"AssetGroup{groupIndex}: checking importer types...", Scale(acc))) return false;
            for (var i = 0; i < targets.Length; i++)
            {
                if (Cancelled()) return false;
                var targetPath = targets[i];
                var name = Path.GetFileName(targetPath);
                var tImp = AssetImporter.GetAtPath(targetPath);
                if (tImp == null) continue;

                for (var k = 1; k <= group.variantsCount; k++)
                {
                    var variantPath = variantsByK[k].FirstOrDefault(v => Path.GetFileName(v) == name);
                    if (variantPath == null) continue;

                    var vImp = AssetImporter.GetAtPath(variantPath);
                    if (vImp == null || vImp.GetType() != tImp.GetType())
                    {
                        Debug.LogError(
                            $"{LogPrefix} Importer type mismatch in AssetGroup{groupIndex}: " +
                            $"'{targetPath}' ({tImp.GetType().Name}) vs " +
                            $"'{variantPath}' ({vImp?.GetType().Name ?? "null"}).");
                        ok = false;
                    }
                }
                if (i % YieldBatchSize == 0) await UniTask.Yield();
            }
            acc += StepWeightTypes;

            // 5) Runtime holders — самый тяжёлый шаг, до 70% времени.
            if (report($"AssetGroup{groupIndex}: scanning runtime holders...", Scale(acc))) return false;
            var variantGuids = variantsByK.Values
                .SelectMany(v => v)
                .Select(AssetDatabase.AssetPathToGUID)
                .Where(g => !string.IsNullOrEmpty(g))
                .ToArray();

            var scanBase = acc;
            var holders = await AssetSwapRuntimeHolderScanner.FindRuntimeHoldersAsync(
                variantGuids,
                onProgress: p =>
                {
                    var globalFrac = Scale(scanBase + StepWeightHolders * p);
                    report($"AssetGroup{groupIndex}: scanning runtime holders ({(int)(p * 100)}%)...", globalFrac);
                },
                ct: ct);

            if (Cancelled()) return false;

            foreach (var (variantPath, holderPath) in holders)
            {
                Debug.LogError(
                    $"{LogPrefix} Runtime asset '{holderPath}' references variant '{variantPath}' " +
                    "— will leak into build (variant must be reachable only from Editor code).");
                ok = false;
            }

            report($"AssetGroup{groupIndex}: done ({(ok ? "OK" : "errors")}).", Scale(1f));
            return ok;
        }

        private static bool CheckUniqueNames(string[] paths, string context)
        {
            var seen = new Dictionary<string, string>();
            var ok = true;
            foreach (var p in paths)
            {
                var name = Path.GetFileName(p);
                if (seen.TryGetValue(name, out var first))
                {
                    Debug.LogError($"{LogPrefix} Duplicate file name '{name}' in {context}: '{first}' and '{p}'.");
                    ok = false;
                }
                else
                {
                    seen[name] = p;
                }
            }
            return ok;
        }
    }
}
