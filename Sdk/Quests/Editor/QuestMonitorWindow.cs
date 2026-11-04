#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Vortex.Sdk.Quests.Conditions;
using Vortex.Sdk.Quests.Conditions.Logics;
using Vortex.Sdk.Quests.Diagnostics;
using Vortex.Sdk.Quests.QuestsLogics;

namespace Vortex.Sdk.Quests.Editor
{
    /// <summary>
    /// Монитор рантайм-состояний квестов: <c>Tools/Vortex/Debug/Quest Monitor</c>. Работает только в Play Mode.
    ///
    /// Окно только показывает данные: модель квестов, след квеста и журнал, которые пишет ядро
    /// (<see cref="QuestDiagnostics"/>). Условия окно не проверяет — результаты взяты из последней штатной
    /// проверки (INV-D1). Дебаг-действия передаются контроллеру (<c>QuestController.ApplyDebugAction</c>).
    /// </summary>
    public class QuestMonitorWindow : EditorWindow
    {
        private enum Tab
        {
            Quest,
            Journal
        }

        private const float ListWidth = 360f;
        private const float RowHeight = 18f;
        private const float ActionsWidth = 160f;

        private static readonly QuestDebugAction[] Actions = (QuestDebugAction[])Enum.GetValues(typeof(QuestDebugAction));

        private static readonly Dictionary<QuestDebugAction, string> ActionLabels = new()
        {
            { QuestDebugAction.Run, "Запустить" },
            { QuestDebugAction.GiveRewards, "Выдать награду" },
            { QuestDebugAction.Recheck, "Перепроверить условия" },
            { QuestDebugAction.CompleteSuccess, "Завершить успехом" },
            { QuestDebugAction.CompleteFail, "Завершить провалом" },
            { QuestDebugAction.Reset, "Сбросить" },
            { QuestDebugAction.Unblock, "Снять блок" },
        };

        private static readonly Color SelectionColor = new(0.24f, 0.48f, 0.9f, 0.35f);
        private static readonly Color ProblemColor = new(0.9f, 0.2f, 0.2f, 0.25f);
        private static readonly Color MutedColor = new(0.55f, 0.55f, 0.55f);
        private static readonly Color TrueColor = new(0.3f, 0.75f, 0.35f);
        private static readonly Color FalseColor = new(0.9f, 0.35f, 0.3f);

        private string _search = "";
        private int _stateMask = -1;
        private string _selectedGuid;
        private Tab _tab;
        private bool _journalOnlySelected;
        private bool _journalOnlyProblems;
        private Vector2 _listScroll;
        private Vector2 _cardScroll;
        private Vector2 _journalScroll;

        /// <summary>GUID пресета → путь ассета; собирается при первом переходе к пресету</summary>
        private Dictionary<string, string> _presetPaths;

        /// <summary>Квесты с исключением в журнале; пересчитывается на каждый OnGUI</summary>
        private readonly HashSet<string> _problemQuests = new();

        [MenuItem("Tools/Vortex/Debug/Quest Monitor")]
        private static void Open()
        {
            var window = GetWindow<QuestMonitorWindow>("Quest Monitor");
            window.minSize = new Vector2(900f, 360f);
        }

        private void OnInspectorUpdate()
        {
            if (Application.isPlaying)
                Repaint();
        }

        private void OnGUI()
        {
            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Монитор квестов доступен в Play Mode.", MessageType.Info);
                return;
            }

            var data = QuestController.DebugData;
            if (data == null)
            {
                EditorGUILayout.HelpBox("Квесты ещё не инициализированы: не было новой игры или загрузки.",
                    MessageType.Info);
                return;
            }

            CollectProblemQuests();
            var quests = data.Index.Values.OrderBy(q => q.QuestName).ToList();

            DrawToolbar(quests);
            using (new EditorGUILayout.HorizontalScope())
            {
                DrawList(quests);
                using (new EditorGUILayout.VerticalScope())
                {
                    _tab = (Tab)GUILayout.Toolbar((int)_tab, new[] { "Квест", "Журнал" }, EditorStyles.toolbarButton);
                    if (_tab == Tab.Quest)
                        DrawCard(data);
                    else
                        DrawJournal(data);
                }
            }
        }

        private void CollectProblemQuests()
        {
            _problemQuests.Clear();
            foreach (var entry in QuestDiagnostics.Journal.Entries())
                if (IsProblem(entry) && entry.QuestGuid != null)
                    _problemQuests.Add(entry.QuestGuid);
        }

        private static bool IsProblem(JournalEntry entry) =>
            entry.Kind == JournalEntryKind.SettleOverflow ||
            (entry.Kind == JournalEntryKind.Transition &&
             entry.Reason is TransitionReason.LogicException or TransitionReason.RewardException);

        #region Панель инструментов

        private void DrawToolbar(List<QuestModel> quests)
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                _search = GUILayout.TextField(_search, EditorStyles.toolbarSearchField, GUILayout.Width(200f));
                _stateMask = EditorGUILayout.MaskField(_stateMask, Enum.GetNames(typeof(QuestState)),
                    EditorStyles.toolbarDropDown, GUILayout.Width(110f));

                var counts = quests.GroupBy(q => q.State).OrderBy(g => g.Key)
                    .Select(g => $"{g.Key} {g.Count()}");
                GUILayout.Label(string.Join(" · ", counts), EditorStyles.miniLabel);
                GUILayout.FlexibleSpace();

                var journal = QuestDiagnostics.Journal;
                var prev = GUI.contentColor;
                if (journal.Evicted > 0)
                    GUI.contentColor = FalseColor;
                GUILayout.Label($"Журнал {journal.Count}/{journal.Capacity}, вытеснено {journal.Evicted}",
                    EditorStyles.miniLabel);
                GUI.contentColor = prev;

                if (GUILayout.Button("Экспорт журнала", EditorStyles.toolbarButton))
                    ExportJournal();
            }
        }

        private static void ExportJournal()
        {
            var now = DateTime.Now;
            var path = EditorUtility.SaveFilePanel("Экспорт журнала квестов", "",
                $"quest-journal-{now:yyyyMMdd-HHmmss}.txt", "txt");
            if (string.IsNullOrEmpty(path))
                return;
            File.WriteAllText(path, QuestDiagnostics.Journal.Export($"Quest Monitor — журнал, {now:yyyy-MM-dd HH:mm:ss}"));
        }

        #endregion

        #region Список

        private void DrawList(List<QuestModel> quests)
        {
            using var scroll = new EditorGUILayout.ScrollViewScope(_listScroll, GUILayout.Width(ListWidth));
            _listScroll = scroll.scrollPosition;

            foreach (var quest in quests)
            {
                if ((_stateMask & (1 << (int)quest.State)) == 0)
                    continue;
                if (_search.Length > 0 &&
                    (quest.QuestName ?? "").IndexOf(_search, StringComparison.OrdinalIgnoreCase) < 0)
                    continue;
                DrawListRow(quest);
            }
        }

        private void DrawListRow(QuestModel quest)
        {
            var rect = EditorGUILayout.GetControlRect(false, RowHeight);
            if (quest.GuidPreset == _selectedGuid)
                EditorGUI.DrawRect(rect, SelectionColor);
            else if (_problemQuests.Contains(quest.GuidPreset))
                EditorGUI.DrawRect(rect, ProblemColor);

            if (Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition))
            {
                _selectedGuid = quest.GuidPreset;
                _tab = Tab.Quest;
                Event.current.Use();
            }

            var trace = QuestDiagnostics.GetTrace(quest.GuidPreset);
            var logic = trace != null && trace.CurrentLogicIndex >= 0
                ? $"{trace.CurrentLogicIndex + 1}/{quest.Logics.Length}"
                : "";
            var flags = $"{(quest.Autorun ? "A" : "·")}{(quest.UnFailable ? "U" : "·")}{(quest.BlockRemovable ? "B" : "·")}";

            var x = rect.x;
            StateLabel(new Rect(x, rect.y, 78f, rect.height), quest.State);
            x += 80f;
            EditorGUI.LabelField(new Rect(x, rect.y, rect.width - 80f - 110f, rect.height), quest.QuestName);
            x = rect.xMax - 108f;
            EditorGUI.LabelField(new Rect(x, rect.y, 40f, rect.height), logic, EditorStyles.miniLabel);
            EditorGUI.LabelField(new Rect(x + 40f, rect.y, 30f, rect.height), flags, EditorStyles.miniLabel);
            EditorGUI.LabelField(new Rect(x + 70f, rect.y, 38f, rect.height), $"s{quest.Step}", EditorStyles.miniLabel);
        }

        #endregion

        #region Карточка

        private void DrawCard(QuestModels data)
        {
            if (_selectedGuid == null || !data.Index.TryGetValue(_selectedGuid, out var quest))
            {
                EditorGUILayout.HelpBox("Выберите квест в списке.", MessageType.None);
                return;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                using (var scroll = new EditorGUILayout.ScrollViewScope(_cardScroll))
                {
                    _cardScroll = scroll.scrollPosition;
                    var trace = QuestDiagnostics.GetTrace(quest.GuidPreset);

                    DrawHeader(quest);
                    DrawLogics(quest, trace);
                    DrawConditions("Условия старта", quest.StartConditions, trace?.LastStartCheck, true);
                    if (quest.InterruptConditions.Length > 0)
                        DrawConditions("Условия прерывания", quest.InterruptConditions, trace?.LastInterruptCheck, false);
                    DrawRewards(quest);
                    DrawQuestJournal(quest);
                }

                DrawActions(quest);
            }
        }

        private static void DrawHeader(QuestModel quest)
        {
            GUILayout.Label(quest.QuestName, EditorStyles.boldLabel);
            EditorGUILayout.SelectableLabel(quest.GuidPreset, EditorStyles.miniLabel, GUILayout.Height(14f));
            using (new EditorGUILayout.HorizontalScope())
            {
                StateLabel(EditorGUILayout.GetControlRect(false, RowHeight, GUILayout.Width(90f)), quest.State);
                GUILayout.Label($"Step {quest.Step}   Autorun: {YesNo(quest.Autorun)}   UnFailable: {YesNo(quest.UnFailable)}" +
                                $"   BlockRemovable: {YesNo(quest.BlockRemovable)}", EditorStyles.miniLabel);
            }
        }

        private static string YesNo(bool value) => value ? "да" : "нет";

        /// <summary>Колонка справа от карточки: дебаг-действия и переход к пресету; не прокручивается вместе с ней</summary>
        private void DrawActions(QuestModel quest)
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(ActionsWidth)))
            {
                foreach (var action in Actions)
                {
                    GUI.enabled = QuestController.IsDebugActionAllowed(quest, action);
                    if (GUILayout.Button(ActionLabels[action]))
                        QuestController.ApplyDebugAction(quest, action);
                }

                GUI.enabled = true;

                EditorGUILayout.Space(4f);
                EditorGUI.DrawRect(EditorGUILayout.GetControlRect(false, 1f), MutedColor);
                EditorGUILayout.Space(4f);

                if (GUILayout.Button("Пресет"))
                    PingPreset(quest.GuidPreset);

                GUILayout.FlexibleSpace();
            }
        }

        private static void DrawLogics(QuestModel quest, QuestRuntimeTrace trace)
        {
            Section("Логики");
            var failed = LastFailure(quest.GuidPreset);
            for (var i = 0; i < quest.Logics.Length; i++)
            {
                var logic = quest.Logics[i];
                var (status, color) = LogicStatus(quest, trace, failed, i);
                using (new EditorGUILayout.HorizontalScope())
                {
                    ColoredLabel(status, color, GUILayout.Width(90f));
                    var label = logic == null ? "null" : logic.EditorLabel;
                    if (logic is SavePoint sp)
                        label += $" #{sp.Key}{(sp.Key == quest.Step ? "  ← Step" : "")}";
                    GUILayout.Label($"{i + 1}. {label}");
                }
            }
        }

        /// <summary>
        /// Последний провал логики квеста из журнала (провал или исключение) — для разметки в терминальном состоянии.
        /// </summary>
        private static JournalEntry LastFailure(string guid) => QuestDiagnostics.Journal.Entries()
            .LastOrDefault(e => e.QuestGuid == guid && e.Kind == JournalEntryKind.Transition &&
                                e.Reason is TransitionReason.LogicFailed or TransitionReason.LogicException);

        private static (string, Color) LogicStatus(QuestModel quest, QuestRuntimeTrace trace, JournalEntry failed, int i)
        {
            switch (quest.State)
            {
                case QuestState.InProgress:
                    var current = trace?.CurrentLogicIndex ?? -1;
                    var restore = trace?.RestoreStartIndex ?? -1;
                    if (i < restore) return ("пропущено", MutedColor);
                    if (current < 0) return ("ожидает", MutedColor);
                    if (i < current) return ("готово", TrueColor);
                    return i == current ? ("выполняется", Color.cyan) : ("ожидает", MutedColor);
                case QuestState.Reward:
                case QuestState.Completed:
                case QuestState.Failed:
                    if (failed != null && failed.LogicIndex >= 0)
                    {
                        if (i < failed.LogicIndex) return ("готово", TrueColor);
                        if (i == failed.LogicIndex)
                            return failed.Reason == TransitionReason.LogicException
                                ? ("исключение", FalseColor)
                                : ("провал", FalseColor);
                        return ("не выполнялось", MutedColor);
                    }

                    return quest.State == QuestState.Failed ? ("—", MutedColor) : ("готово", TrueColor);
                default:
                    return ("ожидает", MutedColor);
            }
        }

        private static void DrawConditions(string title, QuestConditions[] groups, ConditionCheckSnapshot check,
            bool markBlocker)
        {
            Section(check == null
                ? $"{title} — не проверялись"
                : $"{title} — последняя проверка: кадр {check.Frame} ({check.Time:F1} с)");
            if (groups.Length == 0)
            {
                GUILayout.Label("Нет условий", EditorStyles.miniLabel);
                return;
            }

            var blockerFound = !markBlocker;
            var path = new List<int>();
            for (var g = 0; g < groups.Length; g++)
            {
                var group = groups[g];
                path.Add(g);
                var name = string.IsNullOrEmpty(group.Name) ? $"Группа {g + 1}" : group.Name;
                ConditionRow(0, name, Result(check, path), false);
                for (var c = 0; c < group.Conditions.Count; c++)
                {
                    path.Add(c);
                    DrawCondition(group.Conditions[c], check, path, 1, ref blockerFound);
                    path.RemoveAt(path.Count - 1);
                }

                path.RemoveAt(path.Count - 1);
            }
        }

        private static void DrawCondition(QuestConditionLogic condition, ConditionCheckSnapshot check, List<int> path,
            int depth, ref bool blockerFound)
        {
            var result = Result(check, path);
            var blocker = !blockerFound && depth == 1 && result == ConditionResult.False;
            if (blocker)
                blockerFound = true;

            var label = QuestDebugSettings.GetConditionLabel(condition);
            if (condition is LogicGateCondition gate)
                label += $" [{gate.Mode}]";
            ConditionRow(depth, label, result, blocker);

            if (condition is not LogicGateCondition nested)
                return;
            for (var i = 0; i < nested.Conditions.Count; i++)
            {
                path.Add(i);
                DrawCondition(nested.Conditions[i], check, path, depth + 1, ref blockerFound);
                path.RemoveAt(path.Count - 1);
            }
        }

        private static ConditionResult Result(ConditionCheckSnapshot check, List<int> path) =>
            check?.Get(path) ?? ConditionResult.NotEvaluated;

        private static void ConditionRow(int depth, string label, ConditionResult result, bool blocker)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(depth * 16f);
                var (text, color) = result switch
                {
                    ConditionResult.True => ("true", TrueColor),
                    ConditionResult.False => ("false", FalseColor),
                    _ => ("не проверялось", MutedColor)
                };
                ColoredLabel(text, color, GUILayout.Width(90f));
                GUILayout.Label(blocker ? $"{label}   ← блокер" : label,
                    blocker ? EditorStyles.boldLabel : EditorStyles.label);
            }
        }

        private static void DrawRewards(QuestModel quest)
        {
            Section("Награды");
            if (quest.Rewards.Length == 0)
                GUILayout.Label("Нет наград", EditorStyles.miniLabel);
            foreach (var reward in quest.Rewards)
                GUILayout.Label(reward == null ? "null" : reward.EditorLabel);
        }

        private void DrawQuestJournal(QuestModel quest)
        {
            Section("Журнал квеста");
            foreach (var entry in QuestDiagnostics.Journal.Entries().Where(e => e.QuestGuid == quest.GuidPreset)
                         .Reverse())
                DrawJournalRow(entry, false);
        }

        private void PingPreset(string guid)
        {
            _presetPaths ??= AssetDatabase.FindAssets($"t:{nameof(QuestPreset)}")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(p => (path: p, preset: AssetDatabase.LoadAssetAtPath<QuestPreset>(p)))
                .Where(p => p.preset != null)
                .GroupBy(p => p.preset.GuidPreset)
                .ToDictionary(g => g.Key, g => g.First().path);

            if (!_presetPaths.TryGetValue(guid, out var path))
            {
                Debug.LogError($"[QuestMonitor] Пресет квеста с GUID «{guid}» не найден");
                return;
            }

            var asset = AssetDatabase.LoadAssetAtPath<QuestPreset>(path);
            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
        }

        #endregion

        #region Журнал

        private void DrawJournal(QuestModels data)
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                _journalOnlySelected = GUILayout.Toggle(_journalOnlySelected, "Только выбранный квест",
                    EditorStyles.toolbarButton);
                _journalOnlyProblems = GUILayout.Toggle(_journalOnlyProblems, "Только ошибки",
                    EditorStyles.toolbarButton);
                GUILayout.FlexibleSpace();
            }

            using var scroll = new EditorGUILayout.ScrollViewScope(_journalScroll);
            _journalScroll = scroll.scrollPosition;
            foreach (var entry in QuestDiagnostics.Journal.Entries().Reverse())
            {
                if (_journalOnlySelected && entry.QuestGuid != _selectedGuid)
                    continue;
                if (_journalOnlyProblems && !IsProblem(entry))
                    continue;
                DrawJournalRow(entry, true);
            }
        }

        private void DrawJournalRow(JournalEntry entry, bool showQuest)
        {
            var rect = EditorGUILayout.GetControlRect(false, RowHeight);
            if (IsProblem(entry))
                EditorGUI.DrawRect(rect, ProblemColor);
            else if (entry.Kind == JournalEntryKind.Separator)
                EditorGUI.DrawRect(rect, SelectionColor);

            if (showQuest && entry.QuestGuid != null && Event.current.type == EventType.MouseDown &&
                rect.Contains(Event.current.mousePosition))
            {
                _selectedGuid = entry.QuestGuid;
                _tab = Tab.Quest;
                Event.current.Use();
            }

            var text = entry.Kind switch
            {
                JournalEntryKind.Separator => $"──── {entry.Detail} ────",
                JournalEntryKind.SettleOverflow => entry.Detail,
                JournalEntryKind.Note => $"{entry.Reason}",
                _ => $"{entry.From} → {entry.To}   {entry.Reason}"
            };
            if (entry.LogicIndex >= 0)
                text += $"   логика {entry.LogicIndex + 1}";
            if (entry.Kind != JournalEntryKind.Separator && !string.IsNullOrEmpty(entry.Detail))
                text += $"   {entry.Detail}";

            var x = rect.x;
            EditorGUI.LabelField(new Rect(x, rect.y, 50f, rect.height), $"#{entry.Seq}", EditorStyles.miniLabel);
            x += 50f;
            EditorGUI.LabelField(new Rect(x, rect.y, 60f, rect.height), $"f{entry.Frame}", EditorStyles.miniLabel);
            x += 60f;
            if (showQuest)
            {
                EditorGUI.LabelField(new Rect(x, rect.y, 200f, rect.height), entry.QuestName ?? "", EditorStyles.miniLabel);
                x += 200f;
            }

            EditorGUI.LabelField(new Rect(x, rect.y, rect.xMax - x, rect.height), text, EditorStyles.miniLabel);
        }

        #endregion

        #region Отрисовка

        private static void Section(string title)
        {
            EditorGUILayout.Space(6f);
            GUILayout.Label(title, EditorStyles.boldLabel);
        }

        private static void ColoredLabel(string text, Color color, params GUILayoutOption[] options)
        {
            var prev = GUI.contentColor;
            GUI.contentColor = color;
            GUILayout.Label(text, options);
            GUI.contentColor = prev;
        }

        private static void StateLabel(Rect rect, QuestState state)
        {
            var prev = GUI.contentColor;
            GUI.contentColor = StateColor(state);
            EditorGUI.LabelField(rect, state.ToString(), EditorStyles.boldLabel);
            GUI.contentColor = prev;
        }

        private static Color StateColor(QuestState state) => state switch
        {
            QuestState.Ready => new Color(0.95f, 0.8f, 0.25f),
            QuestState.InProgress => Color.cyan,
            QuestState.Reward => new Color(0.85f, 0.45f, 0.95f),
            QuestState.Completed => TrueColor,
            QuestState.Failed => FalseColor,
            QuestState.Blocked => new Color(1f, 0.55f, 0.15f),
            _ => MutedColor
        };

        #endregion
    }
}

#endif
