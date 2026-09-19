using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;

namespace Vortex.Unity.AssetSwapSystem
{
    /// <summary>
    /// Отдельное окно диагностики битового соответствия. Для выбранной группы считает
    /// SHA1 от <c>File.ReadAllBytes</c> для каждого target'а и каждого variant'а
    /// (приоритет — корректность содержимого, а не метки времени, §7 п.6), показывает
    /// таблицу match/— и в summary «All targets match Variant K» / «Mixed».
    ///
    /// SHA1 — I/O-тяжёлая операция; чтобы OnGUI не пересчитывал матрицу на каждый repaint
    /// (hover мыши по окну = сотни File.OpenRead), результат кешируется. Инвалидация:
    /// смена выбранной группы, кнопка <c>Refresh</c>, фокус окна (<see cref="OnFocus"/>).
    /// Изменение выборки/содержимого файлов **вне** этих событий требует явного <c>Refresh</c>.
    ///
    /// Диагностика, не действие. Не трогает файлы.
    /// </summary>
    internal sealed class AssetSwapMatchWindow : EditorWindow
    {
        private int _selectedGroupIndex = -1;
        private Vector2 _scroll;
        private CacheEntry? _cache;

        // GUIStyle нельзя создавать до первого OnGUI (иначе крэш инициализации), поэтому
        // ленивая инициализация через свойство. Статические поля — общие для всех окон.
        private static GUIStyle _matchStyle;
        private static GUIStyle _collisionStyle;

        private static GUIStyle MatchStyle =>
            _matchStyle ??= new GUIStyle(EditorStyles.label)
            {
                normal = { textColor = new Color(0.3f, 0.8f, 0.3f) }
            };

        private static GUIStyle CollisionStyle =>
            _collisionStyle ??= new GUIStyle(EditorStyles.label)
            {
                normal = { textColor = new Color(0.9f, 0.35f, 0.35f) }
            };

        internal static void ShowWindow()
        {
            var window = GetWindow<AssetSwapMatchWindow>("AssetSwap · Match");
            window.minSize = new Vector2(600, 300);
        }

        private void OnFocus() => _cache = null;

        private void OnGUI()
        {
            var settings = AssetSwapSettings.instance;
            var groups = settings.Groups;

            if (groups.Count == 0)
            {
                EditorGUILayout.HelpBox("No groups registered.", MessageType.Info);
                return;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Group:", GUILayout.Width(60));

                var options = groups.Select(g => $"AssetGroup{g.index}").ToArray();
                var currentUiIndex = 0;
                for (var i = 0; i < groups.Count; i++)
                {
                    if (groups[i].index == _selectedGroupIndex)
                    {
                        currentUiIndex = i;
                        break;
                    }
                }

                var newUiIndex = EditorGUILayout.Popup(currentUiIndex, options);
                var newGroupIndex = groups[newUiIndex].index;
                if (newGroupIndex != _selectedGroupIndex)
                {
                    _selectedGroupIndex = newGroupIndex;
                    _cache = null;
                }

                if (GUILayout.Button("Refresh", GUILayout.Width(80)))
                {
                    _cache = null;
                    Repaint();
                }
            }

            var group = settings.GetGroup(_selectedGroupIndex);
            if (group == null)
            {
                _selectedGroupIndex = groups[0].index;
                group = groups[0];
                _cache = null;
            }

            if (!_cache.HasValue
                || _cache.Value.groupIndex != group.index
                || _cache.Value.variantsCount != group.variantsCount)
            {
                _cache = Recompute(group);
            }
            var cache = _cache.Value;

            EditorGUILayout.Space();
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            using (new EditorGUILayout.VerticalScope())
            {
                // Header row.
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("Target", EditorStyles.boldLabel, GUILayout.Width(300));
                    for (var k = 1; k <= group.variantsCount; k++)
                        EditorGUILayout.LabelField($"Variant{k}", EditorStyles.boldLabel, GUILayout.Width(80));
                }

                for (var ti = 0; ti < cache.targets.Length; ti++)
                {
                    var name = Path.GetFileName(cache.targets[ti]);

                    // Сколько вариантов дали match для этой строки — коллизия если >1
                    // (у target'а нет уникального «своего» варианта, варианты байт-идентичны).
                    var rowMatchCount = 0;
                    for (var k = 1; k <= group.variantsCount; k++)
                        if (cache.matrix[ti, k]) rowMatchCount++;
                    var rowCollision = rowMatchCount > 1;

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField(name, GUILayout.Width(300));

                        for (var k = 1; k <= group.variantsCount; k++)
                        {
                            if (cache.matrix[ti, k])
                            {
                                EditorGUILayout.LabelField(
                                    "match",
                                    rowCollision ? CollisionStyle : MatchStyle,
                                    GUILayout.Width(80));
                            }
                            else
                            {
                                EditorGUILayout.LabelField("—", GUILayout.Width(80));
                            }
                        }
                    }
                }

                EditorGUILayout.Space();

                var fullyMatched = cache.matchedPerK
                    .Where(kv => cache.targets.Length > 0 && kv.Value == cache.targets.Length)
                    .Select(kv => kv.Key)
                    .ToArray();

                if (fullyMatched.Length == 1)
                    EditorGUILayout.HelpBox($"All targets match Variant{fullyMatched[0]}.", MessageType.Info);
                else if (fullyMatched.Length > 1)
                    EditorGUILayout.HelpBox(
                        $"Targets match multiple variants: {string.Join(", ", fullyMatched.Select(k => $"Variant{k}"))}. " +
                        "Variants are byte-identical.",
                        MessageType.Warning);
                else
                    EditorGUILayout.HelpBox("Mixed / no full match to a single variant.", MessageType.None);

                EditorGUILayout.LabelField(
                    "Cache refreshes on group change / Refresh button / window focus.",
                    EditorStyles.miniLabel);
            }
            EditorGUILayout.EndScrollView();
        }

        /// <summary>
        /// Полный пересчёт матрицы соответствия: FindAssetsByLabel + SHA1 для каждого файла.
        /// Тяжёлая операция; вызывается только на инвалидации кэша (см. <see cref="OnFocus"/>
        /// и обработку кнопки Refresh / смены группы в <see cref="OnGUI"/>).
        /// </summary>
        private static CacheEntry Recompute(AssetSwapGroup group)
        {
            var targets = AssetSwapLabelHelper.FindAssetsByLabel(AssetSwapLabelHelper.TargetLabel(group.index));
            var variantsByK = new Dictionary<int, string[]>();
            for (var k = 1; k <= group.variantsCount; k++)
                variantsByK[k] = AssetSwapLabelHelper.FindAssetsByLabel(
                    AssetSwapLabelHelper.VariantLabel(group.index, k));

            var matchedPerK = new Dictionary<int, int>();
            for (var k = 1; k <= group.variantsCount; k++) matchedPerK[k] = 0;

            var matrix = new bool[targets.Length, group.variantsCount + 1];
            for (var ti = 0; ti < targets.Length; ti++)
            {
                var targetPath = targets[ti];
                var name = Path.GetFileName(targetPath);
                var targetHash = TryComputeSha1(targetPath);
                if (targetHash == null) continue;

                for (var k = 1; k <= group.variantsCount; k++)
                {
                    var variantPath = variantsByK[k].FirstOrDefault(v => Path.GetFileName(v) == name);
                    if (variantPath == null) continue;

                    var variantHash = TryComputeSha1(variantPath);
                    if (variantHash != null && variantHash == targetHash)
                    {
                        matrix[ti, k] = true;
                        matchedPerK[k] += 1;
                    }
                }
            }

            return new CacheEntry
            {
                groupIndex = group.index,
                variantsCount = group.variantsCount,
                targets = targets,
                variantsByK = variantsByK,
                matrix = matrix,
                matchedPerK = matchedPerK,
            };
        }

        private static string TryComputeSha1(string path)
        {
            try
            {
                using var sha = SHA1.Create();
                using var stream = File.OpenRead(path);
                var hash = sha.ComputeHash(stream);
                return System.BitConverter.ToString(hash).Replace("-", "");
            }
            catch
            {
                return null;
            }
        }

        private struct CacheEntry
        {
            public int groupIndex;
            public int variantsCount;
            public string[] targets;
            public Dictionary<int, string[]> variantsByK;
            public bool[,] matrix;
            public Dictionary<int, int> matchedPerK;
        }
    }
}
