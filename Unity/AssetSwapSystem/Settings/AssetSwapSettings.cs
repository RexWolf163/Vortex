using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Vortex.Unity.AssetSwapSystem
{
    /// <summary>
    /// Настройки AssetSwap. Хранятся в <c>ProjectSettings/VortexAssetSwapSettings.asset</c>
    /// — вне <c>Assets</c>, в билд не попадают, версионируются git. Паттерн — как в
    /// <c>Vortex.Unity.UI.UIBuilder.UIBuilderSettings</c>.
    /// </summary>
    [FilePath("ProjectSettings/VortexAssetSwapSettings.asset", FilePathAttribute.Location.ProjectFolder)]
    internal sealed class AssetSwapSettings : ScriptableSingleton<AssetSwapSettings>
    {
        private const string LogPrefix = "[AssetSwap]";

        [SerializeField] private List<AssetSwapGroup> groups = new();

        internal IReadOnlyList<AssetSwapGroup> Groups => groups;

        internal void SaveToDisk() => Save(true);

        /// <summary>
        /// Минимальный положительный <c>N</c>, не занятый существующими группами.
        /// Не глобальный «текущий+1» — при удалении группы её номер освобождается и переиспользуется.
        /// </summary>
        internal int GetFreeIndex()
        {
            var used = new HashSet<int>();
            foreach (var g in groups)
                if (g != null) used.Add(g.index);
            var n = 1;
            while (used.Contains(n)) n++;
            return n;
        }

        internal AssetSwapGroup GetGroup(int index)
        {
            foreach (var g in groups)
                if (g != null && g.index == index) return g;
            return null;
        }

        internal void AddGroup(AssetSwapGroup group)
        {
            if (GetGroup(group.index) != null)
            {
                Debug.LogError($"{LogPrefix} Duplicate group AssetGroup{group.index} in settings — new one ignored.");
                return;
            }
            groups.Add(group);
        }

        internal void RemoveGroup(int index)
        {
            groups.RemoveAll(g => g == null || g.index == index);
        }

        /// <summary>
        /// Найти и убрать null-элементы и дубликаты по <see cref="AssetSwapGroup.index"/>;
        /// выровнять длину <see cref="AssetSwapGroup.variantComments"/> под
        /// <see cref="AssetSwapGroup.variantsCount"/> у каждой оставшейся группы.
        /// Каждое удаление — <c>Debug.LogError</c> (инвариант I1). Вызывается при открытии
        /// страницы ProjectSettings и в начале любого public API вызова.
        /// </summary>
        internal void Sanitize()
        {
            var seen = new HashSet<int>();
            var kept = new List<AssetSwapGroup>();
            var changed = false;
            foreach (var g in groups)
            {
                if (g == null)
                {
                    changed = true;
                    continue;
                }
                if (!seen.Add(g.index))
                {
                    Debug.LogError($"{LogPrefix} Duplicate group AssetGroup{g.index} in settings — dropped.");
                    changed = true;
                    continue;
                }
                changed |= NormalizeVariantComments(g);
                kept.Add(g);
            }
            if (changed)
            {
                groups = kept;
                SaveToDisk();
            }
        }

        private static bool NormalizeVariantComments(AssetSwapGroup g)
        {
            g.variantComments ??= new List<string>();
            var changed = false;
            while (g.variantComments.Count < g.variantsCount)
            {
                g.variantComments.Add(string.Empty);
                changed = true;
            }
            if (g.variantComments.Count > g.variantsCount)
            {
                g.variantComments.RemoveRange(g.variantsCount, g.variantComments.Count - g.variantsCount);
                changed = true;
            }
            return changed;
        }
    }
}
