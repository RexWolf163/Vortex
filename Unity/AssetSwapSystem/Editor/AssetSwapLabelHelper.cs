using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Vortex.Unity.AssetSwapSystem
{
    /// <summary>
    /// Утилиты работы со штатными Unity Asset Labels: формирование имён по схеме
    /// <c>AssetGroup{N}</c> / <c>AssetGroup{N}_Variant{K}</c>, добавление на выделение,
    /// снятие по имени, поиск по label.
    ///
    /// <b>Ограничение по типу файла:</b> C#-скрипты (<c>*.cs</c>) сознательно исключены
    /// из всех операций — на входе (<see cref="AddLabelToSelected"/>) и на выходе
    /// (<see cref="FindAssetsByLabel"/>). Свап кода <c>File.Copy</c>-ом — плохая идея
    /// (guid.meta и Assembly Definitions рассинхронизируются, compile-фаза непредсказуемо
    /// рвётся посреди Apply), и провоцировать такие эксперименты пакетом не будем.
    /// Ветвление кодовой базы — задача git branch, не AssetSwap.
    /// </summary>
    internal static class AssetSwapLabelHelper
    {
        private const string ScriptExtension = ".cs";

        /// <summary>Имя target-label для группы N.</summary>
        internal static string TargetLabel(int groupIndex) => $"AssetGroup{groupIndex}";

        /// <summary>Имя variant-label для группы N, варианта K.</summary>
        internal static string VariantLabel(int groupIndex, int variantIndex) =>
            $"AssetGroup{groupIndex}_Variant{variantIndex}";

        /// <summary>
        /// Добавить label ко всем ассетам-файлам, выделенным в Project View. Папки и
        /// C#-скрипты пропускаются (см. описание класса). Merge: существующие labels
        /// не сбрасываются, дубликаты не создаются.
        ///
        /// Если выделение пустое — <c>LogWarning</c> и выход: тихий no-op здесь маскирует
        /// «нажал кнопку — ничего не произошло» без объяснения (дизайнер забыл выделить).
        /// Пропущенные .cs-файлы тоже логгируются <c>LogWarning</c>-ом с перечислением
        /// путей — иначе часть выделения молча выпадет из результата.
        ///
        /// В конце — <c>SetDirty</c> + <c>SaveAssets</c> + <c>Refresh</c>.
        /// </summary>
        internal static void AddLabelToSelected(string label)
        {
            if (Selection.assetGUIDs == null || Selection.assetGUIDs.Length == 0)
            {
                Debug.LogWarning($"[AssetSwap] Nothing selected — label '{label}' not applied.");
                return;
            }

            var touched = false;
            var skippedScripts = new List<string>();
            foreach (var guid in Selection.assetGUIDs)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(path)) continue;
                if (AssetDatabase.IsValidFolder(path)) continue;
                if (IsScript(path))
                {
                    skippedScripts.Add(path);
                    continue;
                }

                var asset = AssetDatabase.LoadAssetAtPath<Object>(path);
                if (asset == null) continue;

                var labels = AssetDatabase.GetLabels(asset);
                if (labels.Contains(label)) continue;

                var newLabels = new string[labels.Length + 1];
                for (var i = 0; i < labels.Length; i++) newLabels[i] = labels[i];
                newLabels[^1] = label;

                AssetDatabase.SetLabels(asset, newLabels);
                EditorUtility.SetDirty(asset);
                touched = true;
            }

            if (skippedScripts.Count > 0)
            {
                Debug.LogWarning(
                    $"[AssetSwap] Skipped {skippedScripts.Count} C# script(s) — code files are " +
                    $"not eligible for asset swap (label '{label}' not applied to them): " +
                    string.Join(", ", skippedScripts));
            }

            if (touched)
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
        }

        /// <summary>
        /// Снять несколько labels со всех ассетов проекта одной пачкой: один общий проход
        /// по SetLabels на каждом ассете + один SaveAssets+Refresh в конце. Замена
        /// поштучных вызовов при <c>RemoveGroup</c> (target + K variant-labels): K+1
        /// SaveAssets+Refresh превращается в один — избавляет от секундных пауз на каждом
        /// удалении и от промежуточных half-state'ов (labels снимаются по одному между
        /// fixation'ами).
        ///
        /// Папки и .cs-файлы не фильтруются: если чужая система / дизайнер / прошлый
        /// баг проставил label на них, снимаем тоже — «чистим за собой».
        /// </summary>
        internal static void RemoveLabelsFromAll(IReadOnlyCollection<string> labels)
        {
            if (labels == null || labels.Count == 0) return;

            var touched = false;
            foreach (var label in labels)
            {
                var guids = AssetDatabase.FindAssets($"l:{label}");
                foreach (var guid in guids)
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    var asset = AssetDatabase.LoadAssetAtPath<Object>(path);
                    if (asset == null) continue;

                    var current = AssetDatabase.GetLabels(asset);
                    if (!current.Contains(label)) continue;

                    var filtered = current.Where(l => l != label).ToArray();
                    AssetDatabase.SetLabels(asset, filtered);
                    EditorUtility.SetDirty(asset);
                    touched = true;
                }
            }

            if (touched)
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
        }

        /// <summary>
        /// Пути ассетов-файлов, помеченных данным label. Папки и .cs-скрипты исключены:
        /// свап оперирует только файлами-контентом, .cs — вне ответственности пакета
        /// (см. описание класса). Пустой массив если никого.
        ///
        /// Unity <c>l:X</c>-поиск работает как prefix/substring match (<c>l:AssetGroup1</c>
        /// возвращает и ассеты с <c>AssetGroup1_Variant1</c>), поэтому мы фильтруем
        /// результат по точному наличию label в <see cref="AssetDatabase.GetLabels"/>.
        /// </summary>
        internal static string[] FindAssetsByLabel(string label)
        {
            return AssetDatabase.FindAssets($"l:{label}")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => !string.IsNullOrEmpty(p))
                .Where(p => !AssetDatabase.IsValidFolder(p))
                .Where(p => !IsScript(p))
                .Where(p =>
                {
                    var asset = AssetDatabase.LoadAssetAtPath<Object>(p);
                    return asset != null && AssetDatabase.GetLabels(asset).Contains(label);
                })
                .ToArray();
        }

        private static bool IsScript(string path) =>
            path.EndsWith(ScriptExtension, StringComparison.OrdinalIgnoreCase);

        /// <summary>Показать в Project View выделение всех ассетов с данным label.</summary>
        internal static void PingAssetsByLabel(string label)
        {
            var paths = FindAssetsByLabel(label);
            var objects = paths.Select(AssetDatabase.LoadAssetAtPath<Object>)
                .Where(o => o != null)
                .ToArray();
            Selection.objects = objects;
        }
    }
}