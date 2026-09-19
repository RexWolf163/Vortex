using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Vortex.Unity.AssetSwapSystem
{
    /// <summary>
    /// Public API AssetSwapSystem. Точки входа для страницы ProjectSettings, для окна
    /// Match Window и для CI-batch'ей (метод <see cref="Apply"/> вызывается из внешних
    /// build-хэндлеров, в т.ч. через <c>unity -executeMethod
    /// Vortex.Unity.AssetSwapSystem.AssetSwapController.Apply</c>).
    ///
    /// Все ошибки — <c>Debug.LogError</c> с префиксом <c>[AssetSwap]</c> и указанием
    /// группы/пути; никакого silent-fallback'а (§7 п.9).
    /// </summary>
    public static class AssetSwapController
    {
        private const string LogPrefix = "[AssetSwap]";

        /// <summary>
        /// Зарегистрировать новую группу. Если пользователь выделил ассеты в Project View,
        /// на них сразу проставляется target-label. Возвращает <c>N</c> новой группы.
        /// </summary>
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

        /// <summary>
        /// Добавить новый вариант в группу N. Если выделены ассеты — на них проставится
        /// variant-label. Возвращает индекс <c>K</c> нового варианта; <c>-1</c> при ошибке.
        /// </summary>
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
            settings.SaveToDisk();

            if (Selection.assetGUIDs is { Length: > 0 })
                AssetSwapLabelHelper.AddLabelToSelected(AssetSwapLabelHelper.VariantLabel(groupIndex, k));

            return k;
        }

        /// <summary>
        /// Удалить группу N. Снимает labels <c>AssetGroup{N}</c> и <c>AssetGroup{N}_Variant{K}</c>
        /// со всех помеченных ассетов проекта одной пачкой (один <c>SaveAssets+Refresh</c>
        /// на все <c>K+1</c> labels, не по одному на каждый).
        /// </summary>
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

        /// <summary>
        /// Прогнать валидацию по группе N. Возвращает <c>true</c>, если ошибок нет.
        /// Проверки: уникальность имён внутри target'ов и каждого Variant K; полнота пар
        /// (у target есть пара в каждом Variant); обратка (у variant есть target);
        /// совпадение типов импортёров; отсутствие runtime-держателей variant-ассетов.
        /// </summary>
        public static bool Validate(int groupIndex)
        {
            var settings = AssetSwapSettings.instance;
            var group = settings.GetGroup(groupIndex);
            if (group == null)
            {
                Debug.LogError($"{LogPrefix} Validate: group AssetGroup{groupIndex} not found.");
                return false;
            }

            var ok = true;

            var targets = AssetSwapLabelHelper.FindAssetsByLabel(AssetSwapLabelHelper.TargetLabel(groupIndex));
            var variantsByK = new Dictionary<int, string[]>();
            for (var k = 1; k <= group.variantsCount; k++)
                variantsByK[k] = AssetSwapLabelHelper.FindAssetsByLabel(AssetSwapLabelHelper.VariantLabel(groupIndex, k));

            ok &= CheckUniqueNames(targets, $"AssetGroup{groupIndex} targets");
            foreach (var kv in variantsByK)
                ok &= CheckUniqueNames(kv.Value, $"AssetGroup{groupIndex}_Variant{kv.Key}");

            // Полнота пар.
            foreach (var targetPath in targets)
            {
                var name = Path.GetFileName(targetPath);
                for (var k = 1; k <= group.variantsCount; k++)
                {
                    if (!variantsByK[k].Any(v => Path.GetFileName(v) == name))
                    {
                        Debug.LogError($"{LogPrefix} Target '{targetPath}' has no pair in AssetGroup{groupIndex}_Variant{k}.");
                        ok = false;
                    }
                }
            }

            // Обратка.
            foreach (var kv in variantsByK)
            {
                foreach (var variantPath in kv.Value)
                {
                    var name = Path.GetFileName(variantPath);
                    if (!targets.Any(t => Path.GetFileName(t) == name))
                    {
                        Debug.LogError($"{LogPrefix} Variant '{variantPath}' has no target in AssetGroup{groupIndex}.");
                        ok = false;
                    }
                }
            }

            // Типы импортёров.
            foreach (var targetPath in targets)
            {
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
            }

            // Runtime-держатели вариантов.
            var variantGuids = variantsByK.Values
                                          .SelectMany(v => v)
                                          .Select(AssetDatabase.AssetPathToGUID)
                                          .Where(g => !string.IsNullOrEmpty(g))
                                          .ToArray();
            foreach (var (variantPath, holderPath) in AssetSwapRuntimeHolderScanner.FindRuntimeHolders(variantGuids))
            {
                Debug.LogError(
                    $"{LogPrefix} Runtime asset '{holderPath}' references variant '{variantPath}' " +
                    "— will leak into build (variant must be reachable only from Editor code).");
                ok = false;
            }

            return ok;
        }

        /// <summary>Прогнать <see cref="Validate"/> по всем группам. Возвращает <c>true</c>, если все чистые.</summary>
        public static bool ValidateAll()
        {
            var settings = AssetSwapSettings.instance;
            settings.Sanitize();
            var ok = true;
            foreach (var group in settings.Groups)
                ok &= Validate(group.index);
            return ok;
        }

        /// <summary>
        /// Применить вариант <c>K</c> в группе <c>N</c>: побитово перезаписать содержимое
        /// всех target-файлов их парными variant-файлами. Атомарность batch-reimport'а
        /// обеспечивается <see cref="AssetDatabase.StartAssetEditing"/> /
        /// <see cref="AssetDatabase.StopAssetEditing"/>. <c>.meta</c>-файлы target'ов не трогаются
        /// — GUID сохраняются, все существующие ссылки/Addressables/сцен-разметка целы.
        ///
        /// Валидация запускается автоматически; при ошибках Apply блокируется (§7 п.5).
        /// При частичном сбое <c>File.Copy</c> — LogError, откат через git (§7 п.7).
        /// </summary>
        public static void Apply(int groupIndex, int variantIndex)
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

            if (!Validate(groupIndex))
            {
                Debug.LogError($"{LogPrefix} Apply blocked for AssetGroup{groupIndex}: fix validation errors first.");
                return;
            }

            var targets = AssetSwapLabelHelper.FindAssetsByLabel(AssetSwapLabelHelper.TargetLabel(groupIndex));
            var variants = AssetSwapLabelHelper.FindAssetsByLabel(AssetSwapLabelHelper.VariantLabel(groupIndex, variantIndex));

            var applied = 0;
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (var targetPath in targets)
                {
                    var name = Path.GetFileName(targetPath);
                    var variantPath = variants.FirstOrDefault(v => Path.GetFileName(v) == name);
                    if (variantPath == null) continue; // не должно случиться после Validate

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
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            Debug.Log($"{LogPrefix} Applied AssetGroup{groupIndex} Variant{variantIndex} to {applied}/{targets.Length} assets.");
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
