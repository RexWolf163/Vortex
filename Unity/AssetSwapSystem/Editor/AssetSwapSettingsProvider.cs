using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Vortex.Unity.AssetSwapSystem
{
    /// <summary>
    /// Страница <c>Project Settings → Vortex/AssetSwap</c>: список групп с кнопками
    /// New Group / Add Variant / Remove Group / Validate / Apply Variant K + окно Match.
    /// Все действия делегируются в <see cref="AssetSwapController"/>.
    ///
    /// Тяжёлые операции (Validate / Validate All / Apply) — асинхронные через UniTask
    /// с <c>DisplayCancelableProgressBar</c>: UI живой, лог капает в консоль в реальном
    /// времени, кнопка Cancel в баре прерывает прогон. Пока идёт async-операция,
    /// все кнопки страницы блокируются флагом <see cref="_busy"/> (иначе повторное
    /// нажатие Validate поверх текущего запустит его второй раз и оба будут перетирать
    /// друг другу progress-bar).
    /// </summary>
    internal sealed class AssetSwapSettingsProvider : SettingsProvider
    {
        private const string SettingsPath = "Project/Vortex/AssetSwap";

        // Левый отступ обеих строк варианта (заголовок+Comment и кнопки). Единое
        // значение — чтобы обе строки одного варианта были на одном indent-уровне
        // и варианты визуально «уступали» верхнеуровневой строке Targets группы.
        private const float VariantIndent = 48f;

        // Ввод в поле Comment — потенциальный источник тормозов: каждый keystroke — event,
        // и прямой SaveToDisk на диск за keystroke делает страницу неотзывчивой.
        // Батчим: правки Comment копятся в памяти, флашатся на OnDeactivate (переключение
        // страницы Project Settings) и перед действиями кнопок, которые сами меняют SO.
        private bool _dirty;

        // Идёт async-операция (Validate / Validate All / Apply). Пока true, все кнопки
        // страницы disabled — избегаем повторных запусков поверх текущего прогресс-бара.
        private bool _busy;

        // Счётчики «сколько ассетов помечено каждым label» пересчитываются целиком
        // на структурных изменениях (OnActivate + кнопки New Group / Remove / Add Variant /
        // Add label / Refresh Counts). OnGUI — только чтение из Dictionary. Ввод в Comment
        // OnGUI не удорожает: сканирования Database в перерисовку панели нет.
        private readonly Dictionary<string, int> _counts = new();

        private int GetCount(string label)
            => _counts.TryGetValue(label, out var count) ? count : 0;

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
                "Счётчики Targets / Variant обновляются только при нажатии Validate, " +
                "Validate All или Apply — не автоматически. Каждый из этих проходов " +
                "инкрементально пишет свежие цифры в UI по мере получения (без глобального " +
                "сброса), поэтому цифры чужих групп не мигают. Ввод в Comment и любые " +
                "другие действия счётчики не двигают; при ручной правке labels в инспекторе " +
                "Unity число рядом с меткой станет актуальным лишь после следующей " +
                "валидации.\n\n" +
                "Validate / Validate All / Apply — асинхронные: во время прогона показывается " +
                "progress bar с кнопкой Cancel, лог виден в консоли по мере прохода шагов.",
                MessageType.Info);
            EditorGUILayout.Space();

            using (new EditorGUI.DisabledScope(_busy))
            {
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
                        RunValidateAll().Forget();
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

                // Групповые действия: Add Variant / Validate — над данными (Targets/Variants),
                // чтобы «управление группой» читалось сверху единым блоком с заголовком/Comment,
                // а Targets/Variants ниже оставались чистыми списками данных.
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
                        RunValidate(group.index).Forget();
                    }
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

                // Variants rows. Каждый вариант — box с двумя строками:
                //   (1) «Variant K: N (label)» + Comment TextField до конца строки;
                //   (2) Select | Add label | FlexibleSpace | Apply (Apply отделён от
                //       разрушающих правок Select/AddLabel пустым пространством, чтобы
                //       случайный клик не запустил перезапись файлов).
                // Обрамление helpBox — визуально отделяет варианты друг от друга и делает
                // очевидным, какая пара строк относится к одному варианту. Indent слева
                // задаётся внешним HorizontalScope с Space(VariantIndent) — сам box
                // начинается после отступа и тянется до правого края панели.
                // Комментарий батчится через _dirty ровно как основной group.comment.
                for (var k = 1; k <= group.variantsCount; k++)
                {
                    var variantLabel = AssetSwapLabelHelper.VariantLabel(group.index, k);

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUILayout.Space(VariantIndent);
                        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                        {
                            // (1) заголовок + Comment.
                            using (new EditorGUILayout.HorizontalScope())
                            {
                                EditorGUILayout.LabelField(
                                    $"Variant{k}: {GetCount(variantLabel)} ({variantLabel})",
                                    GUILayout.Width(260 - VariantIndent));

                                var idx = k - 1;
                                var current = idx < group.variantComments.Count
                                    ? group.variantComments[idx]
                                    : string.Empty;
                                EditorGUI.BeginChangeCheck();
                                var next = EditorGUILayout.TextField(current);
                                if (EditorGUI.EndChangeCheck())
                                {
                                    while (group.variantComments.Count <= idx)
                                        group.variantComments.Add(string.Empty);
                                    group.variantComments[idx] = next;
                                    _dirty = true;
                                }
                            }

                            // (2) кнопки.
                            using (new EditorGUILayout.HorizontalScope())
                            {
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
                                GUILayout.FlexibleSpace();
                                if (GUILayout.Button("Apply", GUILayout.Width(70)))
                                {
                                    FlushIfDirty();
                                    var applyGroup = group.index;
                                    var applyK = k;
                                    RunApply(applyGroup, applyK).Forget();
                                }
                            }
                        }
                    }
                }
            }
            EditorGUILayout.Space();
        }

        // === async-обёртки под fire-and-forget из OnGUI. Держат _busy, гарантируют
        // Repaint страницы по завершению, ловят исключения (иначе fire-and-forget
        // проглотит их в дефолтном UniTaskScheduler).

        // Инкрементальный писатель счётчиков: async-операции контроллера прокидывают его
        // в ValidateInternalAsync, тот вызывает WriteCount сразу после FindAssetsByLabel.
        // Без Clear — цифры чужих групп остаются на месте, ничто не «обнуляется на Apply».
        private void WriteCount(string label, int count) => _counts[label] = count;

        private async UniTaskVoid RunValidate(int groupIndex)
        {
            _busy = true;
            try
            {
                var ok = await AssetSwapController.ValidateAsync(groupIndex, showProgressBar: true, WriteCount);
                Debug.Log(ok
                    ? $"[AssetSwap] AssetGroup{groupIndex}: validation passed."
                    : $"[AssetSwap] AssetGroup{groupIndex}: validation failed (or cancelled).");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[AssetSwap] Validate crashed: {e}");
            }
            finally
            {
                _busy = false;
                Repaint();
            }
        }

        private async UniTaskVoid RunValidateAll()
        {
            _busy = true;
            try
            {
                var ok = await AssetSwapController.ValidateAllAsync(showProgressBar: true, WriteCount);
                Debug.Log(ok
                    ? "[AssetSwap] Validation passed."
                    : "[AssetSwap] Validation failed (or cancelled). See errors above.");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[AssetSwap] Validate All crashed: {e}");
            }
            finally
            {
                _busy = false;
                Repaint();
            }
        }

        private async UniTaskVoid RunApply(int groupIndex, int variantIndex)
        {
            _busy = true;
            try
            {
                await AssetSwapController.ApplyAsync(groupIndex, variantIndex, showProgressBar: true, WriteCount);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[AssetSwap] Apply crashed: {e}");
            }
            finally
            {
                _busy = false;
                Repaint();
            }
        }
    }
}
