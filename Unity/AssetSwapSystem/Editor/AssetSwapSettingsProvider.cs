using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Vortex.Unity.AssetSwapSystem
{
    /// <summary>
    /// Страница <c>Project Settings → Vortex/AssetSwap</c>: список групп с кнопками
    /// New Group / Add Variant / Remove Group / Validate / Apply Variant K + окно Match.
    /// Все действия делегируются в <see cref="AssetSwapController"/>.
    /// </summary>
    internal sealed class AssetSwapSettingsProvider : SettingsProvider
    {
        private const string SettingsPath = "Project/Vortex/AssetSwap";

        // Ввод в поле Comment — потенциальный источник тормозов: каждый keystroke — event,
        // и прямой SaveToDisk на диск за keystroke делает страницу неотзывчивой.
        // Батчим: правки Comment копятся в памяти, флашатся на OnDeactivate (переключение
        // страницы Project Settings) и перед действиями кнопок, которые сами меняют SO.
        private bool _dirty;

        // Счётчики «сколько ассетов помечено каждым label» пересчитываются целиком
        // на структурных изменениях (OnActivate + кнопки New Group / Remove / Add Variant /
        // Add label / Refresh Counts). OnGUI — только чтение из Dictionary. Ввод в Comment
        // OnGUI не удорожает: сканирования Database в перерисовку панели нет.
        private readonly Dictionary<string, int> _counts = new();

        private int GetCount(string label)
            => _counts.TryGetValue(label, out var count) ? count : 0;

        private void RefreshCounts()
        {
            _counts.Clear();
            var settings = AssetSwapSettings.instance;
            foreach (var group in settings.Groups)
            {
                var targetLabel = AssetSwapLabelHelper.TargetLabel(group.index);
                _counts[targetLabel] = AssetSwapLabelHelper.FindAssetsByLabel(targetLabel).Length;
                for (var k = 1; k <= group.variantsCount; k++)
                {
                    var vLabel = AssetSwapLabelHelper.VariantLabel(group.index, k);
                    _counts[vLabel] = AssetSwapLabelHelper.FindAssetsByLabel(vLabel).Length;
                }
            }
        }

        private AssetSwapSettingsProvider(string path, SettingsScope scope) : base(path, scope)
        {
        }

        [SettingsProvider]
        private static SettingsProvider Create() =>
            new AssetSwapSettingsProvider(SettingsPath, SettingsScope.Project)
            {
                keywords = new[] { "vortex", "asset", "swap", "variant", "publish", "group", "label" }
            };

        public override void OnActivate(string searchContext, VisualElement rootElement)
        {
            AssetSwapSettings.instance.Sanitize();
            _dirty = false;
            // Счётчики НЕ пересчитываем на Activate — только по явной кнопке Validate.
            // См. HelpBox на странице; кэш пуст → все числа показываются как 0 до Validate.
        }

        public override void OnDeactivate()
        {
            FlushIfDirty();
        }

        private void FlushIfDirty()
        {
            if (!_dirty) return;
            AssetSwapSettings.instance.SaveToDisk();
            _dirty = false;
        }

        public override void OnGUI(string searchContext)
        {
            var settings = AssetSwapSettings.instance;

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(
                "Счётчики Targets / Variant обновляются только при нажатии Validate " +
                "на группе или Validate All — не автоматически. Ввод в Comment и " +
                "любые другие действия не пересчитывают счётчики; при ручной правке labels " +
                "в инспекторе Unity число рядом с меткой станет актуальным лишь после " +
                "следующей валидации.",
                MessageType.Info);
            EditorGUILayout.Space();

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("New Group", GUILayout.Width(120)))
                {
                    FlushIfDirty();
                    var n = AssetSwapController.NewGroup();
                    Debug.Log($"[AssetSwap] Created AssetGroup{n}.");
                }

                if (GUILayout.Button("Validate All", GUILayout.Width(120)))
                {
                    FlushIfDirty();
                    var ok = AssetSwapController.ValidateAll();
                    RefreshCounts();
                    Debug.Log(ok
                        ? "[AssetSwap] Validation passed."
                        : "[AssetSwap] Validation failed. See errors above.");
                }

                if (GUILayout.Button("Match Window", GUILayout.Width(120)))
                {
                    FlushIfDirty();
                    AssetSwapMatchWindow.ShowWindow();
                }
            }

            EditorGUILayout.Space();

            // Копия списка для безопасной итерации: RemoveGroup модифицирует settings.Groups.
            foreach (var group in new List<AssetSwapGroup>(settings.Groups))
                DrawGroup(group);
        }

        private void DrawGroup(AssetSwapGroup group)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField($"AssetGroup{group.index}", EditorStyles.boldLabel);
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("Remove Group", GUILayout.Width(120)))
                    {
                        if (EditorUtility.DisplayDialog(
                                "Remove group",
                                $"Remove AssetGroup{group.index}? " +
                                "All labels of this group will be stripped from project assets. " +
                                "Content of variant-files stays; can be undone via git.",
                                "Remove", "Cancel"))
                        {
                            FlushIfDirty();
                            AssetSwapController.RemoveGroup(group.index);
                            GUIUtility.ExitGUI();
                        }
                    }
                }

                // Comment батчится: keystrokes копятся в памяти, на диск флашатся
                // в OnDeactivate / перед действиями кнопок (см. FlushIfDirty).
                EditorGUI.BeginChangeCheck();
                var comment = EditorGUILayout.TextField("Comment", group.comment ?? string.Empty);
                if (EditorGUI.EndChangeCheck())
                {
                    group.comment = comment;
                    _dirty = true;
                }

                // Targets row.
                using (new EditorGUILayout.HorizontalScope())
                {
                    var targetLabel = AssetSwapLabelHelper.TargetLabel(group.index);
                    EditorGUILayout.LabelField($"Targets: {GetCount(targetLabel)} ({targetLabel})");
                    if (GUILayout.Button("Select in Project", GUILayout.Width(140)))
                    {
                        FlushIfDirty();
                        AssetSwapLabelHelper.PingAssetsByLabel(targetLabel);
                    }
                    if (GUILayout.Button("Add label to Selected", GUILayout.Width(170)))
                    {
                        FlushIfDirty();
                        AssetSwapLabelHelper.AddLabelToSelected(targetLabel);
                    }
                }

                // Variants rows.
                for (var k = 1; k <= group.variantsCount; k++)
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        var variantLabel = AssetSwapLabelHelper.VariantLabel(group.index, k);
                        EditorGUILayout.LabelField($"  Variant{k}: {GetCount(variantLabel)} ({variantLabel})");
                        if (GUILayout.Button("Select", GUILayout.Width(70)))
                        {
                            FlushIfDirty();
                            AssetSwapLabelHelper.PingAssetsByLabel(variantLabel);
                        }
                        if (GUILayout.Button("Add label", GUILayout.Width(80)))
                        {
                            FlushIfDirty();
                            AssetSwapLabelHelper.AddLabelToSelected(variantLabel);
                        }
                        if (GUILayout.Button("Apply", GUILayout.Width(70)))
                        {
                            FlushIfDirty();
                            var applyGroup = group.index;
                            var applyK = k;
                            AssetSwapController.Apply(applyGroup, applyK);
                        }
                    }
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Add Variant", GUILayout.Width(100)))
                    {
                        FlushIfDirty();
                        var newK = AssetSwapController.AddVariant(group.index);
                        if (newK > 0)
                            Debug.Log($"[AssetSwap] AssetGroup{group.index}: added Variant{newK}.");
                    }

                    if (GUILayout.Button("Validate", GUILayout.Width(100)))
                    {
                        FlushIfDirty();
                        var ok = AssetSwapController.Validate(group.index);
                        RefreshCounts();
                        Debug.Log(ok
                            ? $"[AssetSwap] AssetGroup{group.index}: validation passed."
                            : $"[AssetSwap] AssetGroup{group.index}: validation failed.");
                    }
                }
            }
            EditorGUILayout.Space();
        }
    }
}
