using System.Collections.Generic;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Vortex.Core.StateGraphSystem.Model;
using Vortex.Core.StateGraphSystem.Validation;
using Vortex.Unity.EditorTools.EditorSettings;
using Vortex.Unity.StateGraphSystem.Presets;
using Vortex.Unity.StateGraphSystem.Views;

namespace Vortex.Unity.StateGraphSystem.Editor
{
    /// <summary>
    /// Визуализатор графа состояний. Структуру (узлы, переходы, старт) правит в пресете с Undo, позиции узлов —
    /// в раскладке (<see cref="StateGraphLayout"/>). Выбранный узел или переход редактируется в боковой панели
    /// инспектором Odin — так же, как в инспекторе пресета. Замечания проверки подсвечиваются на графе и
    /// перечислены списком. В Play Mode подсвечивается текущий узел у первого выбранного View этого графа,
    /// а если такой не выбран — у выбранного ранее
    /// </summary>
    public class StateGraphWindow : EditorWindow
    {
        private const float ColumnStep = 240f;
        private const float RowStep = 90f;

        [SerializeField] private StateGraphPreset preset;

        private StateGraphLayout _layout;
        private StateGraphCanvas _canvas;
        private ObjectField _presetField;
        private Label _trackLabel;
        private ScrollView _issuesList;
        private PropertyTree _tree;
        private GraphSelection _expandedFor = GraphSelection.None;
        private StateGraphView _trackedView;
        private bool _rebuildScheduled;
        private bool _frameOnBuild;
        private readonly Dictionary<string, Vector2> _autoPositions = new();

        [MenuItem("Tools/Vortex/State Graph Visualizer")]
        private static void OpenEmpty() => GetWindow<StateGraphWindow>("State Graph");

        [InitializeOnLoadMethod]
        private static void Hook() => StateGraphPreset.EditorOpenVisualizer = Open;

        public static void Open(StateGraphPreset target)
        {
            var window = GetWindow<StateGraphWindow>("State Graph");
            window.SetPreset(target);
            window.Focus();
        }

        #region Жизненный цикл окна

        private void OnEnable()
        {
            Undo.undoRedoPerformed += ScheduleRebuild;
            ObjectChangeEvents.changesPublished += OnChangesPublished;
            UnityEditor.Selection.selectionChanged += OnSelectionChanged;
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= ScheduleRebuild;
            ObjectChangeEvents.changesPublished -= OnChangesPublished;
            UnityEditor.Selection.selectionChanged -= OnSelectionChanged;
            DisposeTree();
        }

        private void CreateGUI()
        {
            var toolbar = new Toolbar();
            _presetField = new ObjectField { objectType = typeof(StateGraphPreset), allowSceneObjects = false };
            _presetField.style.minWidth = 220;
            _presetField.RegisterValueChangedCallback(evt => SetPreset(evt.newValue as StateGraphPreset));
            toolbar.Add(_presetField);
            toolbar.Add(new ToolbarButton(() => AddNode(_canvas.ViewCenter())) { text = "Add node" });
            toolbar.Add(new ToolbarButton(() => _canvas.FrameAll()) { text = "Frame all" });
            toolbar.Add(new ToolbarSpacer { flex = true });
            _trackLabel = new Label();
            _trackLabel.style.unityTextAlign = TextAnchor.MiddleRight;
            toolbar.Add(_trackLabel);
            rootVisualElement.Add(toolbar);

            var split = new TwoPaneSplitView(1, 340f, TwoPaneSplitViewOrientation.Horizontal);
            split.style.flexGrow = 1;

            _canvas = new StateGraphCanvas();
            _canvas.SelectionChanged += _ => Repaint();
            _canvas.NodeMoved += SetPosition;
            _canvas.Connected += AddTransition;
            _canvas.ContextRequested += ShowContextMenu;
            _canvas.DeleteRequested += DeleteSelection;
            _canvas.RegisterCallback<GeometryChangedEvent>(_ =>
            {
                if (!_frameOnBuild)
                    return;
                _frameOnBuild = false;
                _canvas.FrameAll();
            });
            split.Add(_canvas);

            var side = new VisualElement();
            side.style.minWidth = 200;
            var inspector = new IMGUIContainer(DrawInspector);
            inspector.style.flexGrow = 1;
            var inspectorScroll = new ScrollView();
            inspectorScroll.style.flexGrow = 1;
            inspectorScroll.Add(inspector);
            side.Add(inspectorScroll);
            var issuesHeader = new Label("Issues");
            issuesHeader.style.unityFontStyleAndWeight = FontStyle.Bold;
            issuesHeader.style.marginLeft = 4;
            issuesHeader.style.marginTop = 4;
            side.Add(issuesHeader);
            _issuesList = new ScrollView();
            _issuesList.style.maxHeight = 220;
            side.Add(_issuesList);
            split.Add(side);

            rootVisualElement.Add(split);
            rootVisualElement.schedule.Execute(UpdatePlayHighlight).Every(150);

            _presetField.SetValueWithoutNotify(preset);
            LoadLayout();
            _frameOnBuild = true;
            Rebuild();
        }

        private void SetPreset(StateGraphPreset target)
        {
            if (preset == target && _canvas != null)
                return;
            preset = target;
            DisposeTree();
            _presetField?.SetValueWithoutNotify(preset);
            _trackedView = null;
            LoadLayout();
            _canvas?.Select(GraphSelection.None);
            Rebuild();
            _canvas?.FrameAll();
        }

        private void LoadLayout()
        {
            _layout = preset == null ? null : StateGraphLayout.Find(preset);
            _autoPositions.Clear();
        }

        private void OnChangesPublished(ref ObjectChangeEventStream stream) => ScheduleRebuild();

        private void ScheduleRebuild()
        {
            if (_rebuildScheduled || rootVisualElement == null)
                return;
            _rebuildScheduled = true;
            rootVisualElement.schedule.Execute(() =>
            {
                _rebuildScheduled = false;
                Rebuild();
            });
        }

        private void DisposeTree()
        {
            _tree?.Dispose();
            _tree = null;
        }

        #endregion

        #region Построение графа

        private void Rebuild()
        {
            if (_canvas == null)
                return;

            _issuesList.Clear();
            if (preset == null)
            {
                _canvas.Build(new CanvasNode[0], new CanvasEdge[0], GraphSelection.None);
                Repaint();
                return;
            }

            preset.EditorRefresh();
            var issues = preset.EditorCollectIssues();
            var nodes = new List<CanvasNode>();
            var edges = new List<CanvasEdge>();
            var ids = new HashSet<string>();
            foreach (var node in preset.RawNodes)
                if (node != null)
                    ids.Add(node.Id);

            foreach (var node in preset.RawNodes)
            {
                if (node == null)
                    continue;

                var transitions = node.RawTransitions;
                var order = 0;
                for (var t = 0; t < transitions.Length; t++)
                {
                    var transition = transitions[t];
                    if (transition == null)
                        continue;
                    order++;
                    if (!ids.Contains(transition.Target))
                        continue;
                    edges.Add(new CanvasEdge
                    {
                        From = node.Id,
                        To = transition.Target,
                        Transition = t,
                        Order = order,
                        WaitNext = transition.WaitNext,
                        Conditions = transition.Conditions.Length,
                        Issue = WorstIssue(issues, node.Id, t)
                    });
                }

                nodes.Add(new CanvasNode
                {
                    Id = node.Id,
                    Label = node.Label,
                    IsStart = preset.StartNode == node.Id,
                    IsEnd = transitions.Length == 0,
                    Issue = WorstNodeIssue(issues, node, ids),
                    Position = GetPosition(node.Id)
                });
            }

            var selection = _canvas.Selection;
            if (!SelectionExists(selection))
                selection = GraphSelection.None;
            _canvas.Build(nodes, edges, selection);
            FillIssues(issues);
            Repaint();
        }

        /// <summary>Худшее замечание по переходу</summary>
        private static IssueSeverity? WorstIssue(List<StateGraphPresetIssue> issues, string nodeId, int transition)
        {
            IssueSeverity? worst = null;
            foreach (var issue in issues)
                if (issue.NodeId == nodeId && issue.TransitionIndex == transition &&
                    (worst == null || issue.Severity > worst))
                    worst = issue.Severity;
            return worst;
        }

        /// <summary>
        /// Худшее замечание по узлу: свои замечания узла и замечания переходов, которые не нарисованы
        /// (переход в никуда) — иначе их не было бы видно на графе
        /// </summary>
        private static IssueSeverity? WorstNodeIssue(List<StateGraphPresetIssue> issues, StateNodePreset node,
            HashSet<string> ids)
        {
            IssueSeverity? worst = null;
            foreach (var issue in issues)
            {
                if (issue.NodeId != node.Id)
                    continue;
                var drawn = issue.TransitionIndex >= 0 && issue.TransitionIndex < node.RawTransitions.Length &&
                            node.RawTransitions[issue.TransitionIndex] != null &&
                            ids.Contains(node.RawTransitions[issue.TransitionIndex].Target);
                if (!drawn && (worst == null || issue.Severity > worst))
                    worst = issue.Severity;
            }

            return worst;
        }

        private bool SelectionExists(GraphSelection selection)
        {
            if (selection.IsEmpty)
                return true;
            var node = preset.FindNode(selection.NodeId);
            if (node == null)
                return false;
            return selection.IsNode || (selection.Transition < node.RawTransitions.Length &&
                                        node.RawTransitions[selection.Transition] != null);
        }

        private void FillIssues(List<StateGraphPresetIssue> issues)
        {
            if (issues.Count == 0)
            {
                _issuesList.Add(new Label("Graph is valid.") { style = { marginLeft = 4 } });
                return;
            }

            issues.Sort((a, b) => b.Severity.CompareTo(a.Severity));
            foreach (var issue in issues)
            {
                var label = new Label(issue.Message);
                label.style.whiteSpace = WhiteSpace.Normal;
                label.style.marginLeft = 4;
                label.style.marginBottom = 2;
                label.style.color = StateGraphCanvas.Palette(issue.Severity == IssueSeverity.Error
                    ? DefaultColors.GraphError
                    : DefaultColors.GraphWarning);
                if (issue.NodeId != null)
                {
                    var selection = new GraphSelection(issue.NodeId, issue.TransitionIndex);
                    label.RegisterCallback<PointerDownEvent>(_ => _canvas.Select(selection));
                }

                _issuesList.Add(label);
            }
        }

        #endregion

        #region Позиции

        private Vector2 GetPosition(string nodeId)
        {
            if (_layout != null && _layout.TryGetPosition(nodeId, out var position))
                return position;
            if (!_autoPositions.TryGetValue(nodeId, out position))
            {
                ComputeAutoPositions();
                _autoPositions.TryGetValue(nodeId, out position);
            }

            return position;
        }

        /// <summary>
        /// Начальная раскладка для узлов без сохранённой позиции: столбец — глубина от старта (обход в ширину),
        /// строка — порядок в столбце; недостижимые — отдельным столбцом справа
        /// </summary>
        private void ComputeAutoPositions()
        {
            _autoPositions.Clear();
            var depth = new Dictionary<string, int>();
            var queue = new Queue<string>();
            if (preset.FindNode(preset.StartNode) != null)
            {
                depth[preset.StartNode] = 0;
                queue.Enqueue(preset.StartNode);
            }

            while (queue.Count > 0)
            {
                var id = queue.Dequeue();
                foreach (var transition in preset.FindNode(id).RawTransitions)
                {
                    if (transition == null || depth.ContainsKey(transition.Target ?? "") ||
                        preset.FindNode(transition.Target) == null)
                        continue;
                    depth[transition.Target] = depth[id] + 1;
                    queue.Enqueue(transition.Target);
                }
            }

            var maxDepth = 0;
            foreach (var value in depth.Values)
                maxDepth = Mathf.Max(maxDepth, value);

            var rows = new Dictionary<int, int>();
            foreach (var node in preset.RawNodes)
            {
                if (node == null)
                    continue;
                var column = depth.TryGetValue(node.Id, out var d) ? d : maxDepth + 1;
                rows.TryGetValue(column, out var row);
                rows[column] = row + 1;
                _autoPositions[node.Id] = new Vector2(column * ColumnStep, row * RowStep);
            }
        }

        private void SetPosition(string nodeId, Vector2 position)
        {
            if (preset == null)
                return;
            if (_layout == null)
                _layout = StateGraphLayout.Create(preset);
            Undo.RecordObject(_layout, "Move state graph node");
            _layout.SetPosition(nodeId, position);
            EditorUtility.SetDirty(_layout);
        }

        #endregion

        #region Правки структуры

        private void AddNode(Vector2 position)
        {
            if (preset == null)
                return;
            var group = Undo.GetCurrentGroup();
            Undo.RecordObject(preset, "Add state graph node");
            var node = preset.EditorAddNode(preset.EditorUniqueName("Node"));
            EditorUtility.SetDirty(preset);
            SetPosition(node.Id, position);
            Undo.CollapseUndoOperations(group);
            Rebuild();
            _canvas.Select(new GraphSelection(node.Id, -1));
        }

        private void DeleteNode(string nodeId)
        {
            var group = Undo.GetCurrentGroup();
            Undo.RecordObject(preset, "Delete state graph node");
            preset.EditorRemoveNode(nodeId);
            EditorUtility.SetDirty(preset);
            if (_layout != null)
            {
                Undo.RecordObject(_layout, "Delete state graph node");
                _layout.RemovePosition(nodeId);
                EditorUtility.SetDirty(_layout);
            }

            Undo.CollapseUndoOperations(group);
            _canvas.Select(GraphSelection.None);
            Rebuild();
        }

        private void AddTransition(string fromId, string toId)
        {
            var node = preset?.FindNode(fromId);
            if (node == null)
                return;
            Undo.RecordObject(preset, "Add state graph transition");
            node.EditorAddTransition(toId);
            preset.EditorRefresh();
            EditorUtility.SetDirty(preset);
            Rebuild();
            _canvas.Select(new GraphSelection(fromId, node.RawTransitions.Length - 1));
        }

        private void DeleteTransition(string fromId, int transition)
        {
            Undo.RecordObject(preset, "Delete state graph transition");
            preset.FindNode(fromId).EditorRemoveTransition(transition);
            preset.EditorRefresh();
            EditorUtility.SetDirty(preset);
            _canvas.Select(GraphSelection.None);
            Rebuild();
        }

        private void ToggleWaitNext(string fromId, int transition)
        {
            var target = preset.FindNode(fromId).RawTransitions[transition];
            Undo.RecordObject(preset, "Toggle state graph transition Next");
            target.EditorSetWaitNext(!target.WaitNext);
            EditorUtility.SetDirty(preset);
            Rebuild();
        }

        private void SetStart(string nodeId)
        {
            Undo.RecordObject(preset, "Set state graph start node");
            preset.EditorSetStartNode(nodeId);
            EditorUtility.SetDirty(preset);
            Rebuild();
        }

        private void DeleteSelection()
        {
            var selection = _canvas.Selection;
            if (preset == null || selection.IsEmpty)
                return;
            if (selection.IsNode)
                DeleteNode(selection.NodeId);
            else
                DeleteTransition(selection.NodeId, selection.Transition);
        }

        private void ShowContextMenu(GraphSelection selection, Vector2 point)
        {
            if (preset == null)
                return;

            var menu = new GenericMenu();
            if (selection.IsNode)
            {
                var id = selection.NodeId;
                if (preset.StartNode == id)
                    menu.AddDisabledItem(new GUIContent("Set as start"));
                else
                    menu.AddItem(new GUIContent("Set as start"), false, () => SetStart(id));
                menu.AddItem(new GUIContent("Delete node"), false, () => DeleteNode(id));
            }
            else if (selection.IsEdge)
            {
                var id = selection.NodeId;
                var transition = selection.Transition;
                var waitNext = preset.FindNode(id).RawTransitions[transition].WaitNext;
                menu.AddItem(new GUIContent("Wait Next"), waitNext, () => ToggleWaitNext(id, transition));
                menu.AddItem(new GUIContent("Delete transition"), false, () => DeleteTransition(id, transition));
            }
            else
                menu.AddItem(new GUIContent("Add node"), false,
                    () => AddNode(point - new Vector2(StateGraphCanvas.NodeWidth, StateGraphCanvas.NodeHeight) * 0.5f));

            menu.ShowAsContext();
        }

        #endregion

        #region Боковая панель

        private void DrawInspector()
        {
            if (preset == null)
            {
                EditorGUILayout.HelpBox("Select a State Graph preset.", MessageType.Info);
                return;
            }

            var selection = _canvas?.Selection ?? GraphSelection.None;
            if (selection.IsEmpty)
            {
                EditorGUILayout.HelpBox(
                    "Select a node or a transition to edit it.\n" +
                    "Drag the node's right handle onto another node to add a transition.\n" +
                    "Right click — context menu, Delete — remove the selection.", MessageType.Info);
                return;
            }

            var index = preset.EditorIndexOf(selection.NodeId);
            if (index < 0)
                return;

            _tree ??= PropertyTree.Create(preset);
            _tree.BeginDraw(true);
            var path = selection.IsNode ? $"nodes.${index}" : $"nodes.${index}.transitions.${selection.Transition}";
            var unityPath = selection.IsNode
                ? $"nodes.Array.data[{index}]"
                : $"nodes.Array.data[{index}].transitions.Array.data[{selection.Transition}]";
            var property = _tree.GetPropertyAtPath(path) ?? _tree.GetPropertyAtUnityPath(unityPath);
            if (property == null)
                EditorGUILayout.HelpBox($"Property «{path}» not found.", MessageType.Warning);
            else
            {
                // Раскрыть выделенное один раз при выборе: дальше пользователь сворачивает сам
                if (!_expandedFor.Equals(selection))
                {
                    _expandedFor = selection;
                    property.State.Expanded = true;
                    foreach (var child in property.Children)
                        child.State.Expanded = true;
                }

                EditorGUI.BeginChangeCheck();
                // Без собственного заголовка элемента массива («Nodes [i]») — сразу содержимое
                property.Draw(null);
                if (EditorGUI.EndChangeCheck())
                    ScheduleRebuild();
            }

            _tree.EndDraw();
        }

        #endregion

        #region Play Mode

        private void OnSelectionChanged()
        {
            if (preset == null)
                return;
            foreach (var gameObject in UnityEditor.Selection.gameObjects)
            {
                var view = gameObject.GetComponent<StateGraphView>();
                if (view == null || view.EditorGraphGuid != preset.GuidPreset)
                    continue;
                _trackedView = view;
                return;
            }
        }

        private void UpdatePlayHighlight()
        {
            if (_canvas == null)
                return;

            string current = null;
            var tracked = _trackedView != null && preset != null && _trackedView.EditorGraphGuid == preset.GuidPreset;
            if (Application.isPlaying && tracked)
                current = _trackedView.GetData<StateGraph>()?.CurrentNode.Value;

            _canvas.SetCurrent(string.IsNullOrEmpty(current) ? null : current);
            _trackLabel.text = tracked ? $"View: {_trackedView.name}" : "";
        }

        #endregion
    }
}
