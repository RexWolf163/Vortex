using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using Vortex.Core.StateGraphSystem.Validation;
using Vortex.Unity.EditorTools.EditorSettings;

namespace Vortex.Unity.StateGraphSystem.Editor
{
    /// <summary>Выделение на полотне: узел (Transition = -1), переход узла (сырой номер) или ничего</summary>
    internal readonly struct GraphSelection : IEquatable<GraphSelection>
    {
        public readonly string NodeId;
        public readonly int Transition;

        public static readonly GraphSelection None = new(null, -1);

        public GraphSelection(string nodeId, int transition)
        {
            NodeId = nodeId;
            Transition = transition;
        }

        public bool IsEmpty => NodeId == null;
        public bool IsNode => NodeId != null && Transition < 0;
        public bool IsEdge => NodeId != null && Transition >= 0;

        public bool Equals(GraphSelection other) => NodeId == other.NodeId && Transition == other.Transition;
        public override bool Equals(object obj) => obj is GraphSelection other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(NodeId, Transition);
    }

    /// <summary>Данные узла для отрисовки</summary>
    internal sealed class CanvasNode
    {
        public string Id;
        public string Label;
        public bool IsStart;
        public bool IsEnd;
        public IssueSeverity? Issue;
        public Vector2 Position;
    }

    /// <summary>Данные перехода для отрисовки</summary>
    internal sealed class CanvasEdge
    {
        public string From;
        public string To;
        public int Transition;
        public int Order;
        public bool WaitNext;
        public int Conditions;
        public IssueSeverity? Issue;

        internal readonly List<Vector2> Points = new();
        internal Label LabelElement;
    }

    /// <summary>
    /// Полотно графа на UI Toolkit: узлы — элементы, переходы — ломаные, рисуемые Painter2D.
    /// Только представление: данные и правки — у окна (события наружу).
    /// Управление: ЛКМ по фону / СКМ — сдвиг, колесо — масштаб, перетаскивание узла — позиция,
    /// протягивание от «порта» узла к другому узлу — новый переход, ПКМ — контекстное меню, Delete — удаление
    /// </summary>
    internal sealed class StateGraphCanvas : VisualElement
    {
        public const float NodeWidth = 170f;
        public const float NodeHeight = 46f;
        private const float MinZoom = 0.25f;
        private const float MaxZoom = 2f;
        private const float EdgeHitDistance = 6f;
        private const float ParallelOffset = 14f;

        /// <summary>Цвет палитры графа из ToolsSettings (по теме редактора)</summary>
        internal static Color Palette(DefaultColors key) => ToolsSettings.GetBgColor(key);

        public event Action<GraphSelection> SelectionChanged;
        public event Action<string, Vector2> NodeMoved;
        public event Action<string, string> Connected;

        /// <summary>Контекстное меню: что под курсором (None — фон) и точка в координатах графа</summary>
        public event Action<GraphSelection, Vector2> ContextRequested;

        public event Action DeleteRequested;

        private readonly VisualElement _content;
        private readonly VisualElement _edgeLayer;
        private readonly Dictionary<string, NodeElement> _nodes = new();
        private readonly Dictionary<string, CanvasNode> _nodeData = new();
        private readonly List<CanvasEdge> _edges = new();

        private Vector2 _pan = new(40f, 40f);
        private float _zoom = 1f;
        private Rect _edgeBounds;

        private GraphSelection _selection = GraphSelection.None;
        private string _current;

        private bool _panning;
        private Vector2 _panStartMouse;
        private Vector2 _panStartPan;

        private string _connectFrom;
        private Vector2 _connectPoint;

        public GraphSelection Selection => _selection;

        public StateGraphCanvas()
        {
            style.flexGrow = 1;
            style.overflow = Overflow.Hidden;
            style.backgroundColor = Palette(DefaultColors.GraphBg);
            focusable = true;

            _content = new VisualElement { pickingMode = PickingMode.Ignore };
            _content.style.position = Position.Absolute;
            _content.style.left = 0;
            _content.style.top = 0;
            _content.style.transformOrigin = new TransformOrigin(0, 0);
            Add(_content);

            _edgeLayer = new VisualElement { pickingMode = PickingMode.Ignore };
            _edgeLayer.style.position = Position.Absolute;
            _edgeLayer.generateVisualContent += DrawEdges;
            _content.Add(_edgeLayer);

            RegisterCallback<PointerDownEvent>(OnPointerDown);
            RegisterCallback<PointerMoveEvent>(OnPointerMove);
            RegisterCallback<PointerUpEvent>(OnPointerUp);
            RegisterCallback<WheelEvent>(OnWheel);
            RegisterCallback<KeyDownEvent>(OnKeyDown);

            ApplyTransform();
        }

        #region Построение

        /// <summary>Перестроить полотно по данным (сдвиг и масштаб сохраняются)</summary>
        public void Build(IEnumerable<CanvasNode> nodes, IEnumerable<CanvasEdge> edges, GraphSelection selection)
        {
            // Палитру могли поменять в ToolsSettings
            style.backgroundColor = Palette(DefaultColors.GraphBg);
            foreach (var node in _nodes.Values)
                node.RemoveFromHierarchy();
            foreach (var edge in _edges)
                edge.LabelElement?.RemoveFromHierarchy();
            _nodes.Clear();
            _nodeData.Clear();
            _edges.Clear();

            foreach (var data in nodes)
            {
                _nodeData[data.Id] = data;
                var element = new NodeElement(this, data);
                _nodes[data.Id] = element;
                _content.Add(element);
            }

            foreach (var edge in edges)
            {
                if (!_nodeData.ContainsKey(edge.From) || !_nodeData.ContainsKey(edge.To))
                    continue;
                edge.LabelElement = CreateEdgeLabel(edge);
                _content.Add(edge.LabelElement);
                _edges.Add(edge);
            }

            _selection = selection;
            RecalculateEdges();
            RefreshStyles();
        }

        /// <summary>Подсветить текущий узел исполнения (null — без подсветки)</summary>
        public void SetCurrent(string nodeId)
        {
            if (_current == nodeId)
                return;
            _current = nodeId;
            RefreshStyles();
        }

        public void Select(GraphSelection selection)
        {
            if (_selection.Equals(selection))
                return;
            _selection = selection;
            RefreshStyles();
            SelectionChanged?.Invoke(selection);
        }

        /// <summary>Вписать весь граф в окно</summary>
        public void FrameAll()
        {
            if (_nodeData.Count == 0 || float.IsNaN(layout.width) || layout.width <= 0 || layout.height <= 0)
                return;

            var bounds = GetNodesBounds();
            const float margin = 40f;
            _zoom = Mathf.Clamp(Mathf.Min((layout.width - margin * 2) / bounds.width,
                (layout.height - margin * 2) / bounds.height), MinZoom, 1f);
            _pan = new Vector2(layout.width, layout.height) * 0.5f - bounds.center * _zoom;
            ApplyTransform();
        }

        /// <summary>Центр видимой области в координатах графа</summary>
        public Vector2 ViewCenter() =>
            (new Vector2(layout.width, layout.height) * 0.5f - _pan) / _zoom - new Vector2(NodeWidth, NodeHeight) * 0.5f;

        private Rect GetNodesBounds()
        {
            var min = new Vector2(float.MaxValue, float.MaxValue);
            var max = new Vector2(float.MinValue, float.MinValue);
            foreach (var node in _nodeData.Values)
            {
                min = Vector2.Min(min, node.Position);
                max = Vector2.Max(max, node.Position + new Vector2(NodeWidth, NodeHeight));
            }

            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        private Label CreateEdgeLabel(CanvasEdge edge)
        {
            var text = edge.Order.ToString();
            if (edge.WaitNext)
                text += " Next";
            if (edge.Conditions > 0)
                text += $" [{edge.Conditions}]";

            var label = new Label(text);
            label.style.position = Position.Absolute;
            label.style.fontSize = 10;
            label.style.paddingLeft = 3;
            label.style.paddingRight = 3;
            label.style.borderTopLeftRadius = 3;
            label.style.borderTopRightRadius = 3;
            label.style.borderBottomLeftRadius = 3;
            label.style.borderBottomRightRadius = 3;
            label.style.backgroundColor = Palette(DefaultColors.GraphLabelBg);
            label.style.color = edge.WaitNext ? Palette(DefaultColors.GraphEdgeNext) : Palette(DefaultColors.GraphNodeText);
            label.style.borderTopWidth = 1;
            label.style.borderBottomWidth = 1;
            label.style.borderLeftWidth = 1;
            label.style.borderRightWidth = 1;
            var labelBorder = edge.WaitNext ? Palette(DefaultColors.GraphEdgeNext) : Palette(DefaultColors.GraphEdge);
            label.style.borderTopColor = labelBorder;
            label.style.borderBottomColor = labelBorder;
            label.style.borderLeftColor = labelBorder;
            label.style.borderRightColor = labelBorder;
            label.style.translate = new Translate(Length.Percent(-50), Length.Percent(-50));
            label.RegisterCallback<PointerDownEvent>(evt =>
            {
                var selection = new GraphSelection(edge.From, edge.Transition);
                if (evt.button == 0)
                {
                    Focus();
                    Select(selection);
                    evt.StopPropagation();
                }
                else if (evt.button == 1)
                {
                    Select(selection);
                    ContextRequested?.Invoke(selection, ContentPoint(evt.position));
                    evt.StopPropagation();
                }
            });
            return label;
        }

        #endregion

        #region Геометрия переходов

        private void RecalculateEdges()
        {
            var groups = new Dictionary<(string, string), List<CanvasEdge>>();
            foreach (var edge in _edges)
            {
                if (!groups.TryGetValue((edge.From, edge.To), out var group))
                    groups[(edge.From, edge.To)] = group = new List<CanvasEdge>();
                group.Add(edge);
            }

            foreach (var pair in groups)
            {
                var (from, to) = pair.Key;
                var group = pair.Value;
                var reverse = from != to && groups.ContainsKey((to, from));
                for (var k = 0; k < group.Count; k++)
                {
                    var edge = group[k];
                    edge.Points.Clear();
                    if (from == to)
                        BuildLoop(edge, _nodeData[from].Position, k);
                    else
                    {
                        var offset = (k - (group.Count - 1) * 0.5f) * ParallelOffset + (reverse ? ParallelOffset * 0.5f : 0f);
                        BuildLine(edge, _nodeData[from].Position, _nodeData[to].Position, offset);
                    }

                    edge.LabelElement.style.left = Midpoint(edge.Points).x;
                    edge.LabelElement.style.top = Midpoint(edge.Points).y;
                }
            }

            UpdateEdgeLayerBounds();
        }

        private static void BuildLine(CanvasEdge edge, Vector2 fromPos, Vector2 toPos, float offset)
        {
            var size = new Vector2(NodeWidth, NodeHeight);
            var a = fromPos + size * 0.5f;
            var b = toPos + size * 0.5f;
            var direction = (b - a).normalized;
            if (direction == Vector2.zero)
                direction = Vector2.right;
            var perpendicular = new Vector2(-direction.y, direction.x) * offset;
            var fromRect = new Rect(fromPos, size);
            var toRect = new Rect(toPos, size);
            edge.Points.Add(ExitPoint(fromRect, ClampInside(fromRect, a + perpendicular), direction));
            edge.Points.Add(ExitPoint(toRect, ClampInside(toRect, b + perpendicular), -direction));
        }

        /// <summary>Петля на себя — дуга над узлом</summary>
        private static void BuildLoop(CanvasEdge edge, Vector2 position, int index)
        {
            var height = 40f + index * 16f;
            var p0 = new Vector2(position.x + NodeWidth * 0.62f, position.y);
            var p3 = new Vector2(position.x + NodeWidth * 0.38f, position.y);
            var p1 = p0 + new Vector2(30f, -height);
            var p2 = p3 + new Vector2(-30f, -height);
            const int samples = 16;
            for (var i = 0; i <= samples; i++)
            {
                var t = i / (float)samples;
                var u = 1f - t;
                edge.Points.Add(u * u * u * p0 + 3f * u * u * t * p1 + 3f * u * t * t * p2 + t * t * t * p3);
            }
        }

        private static Vector2 ClampInside(Rect rect, Vector2 point) =>
            new(Mathf.Clamp(point.x, rect.xMin + 2f, rect.xMax - 2f), Mathf.Clamp(point.y, rect.yMin + 2f, rect.yMax - 2f));

        /// <summary>Точка выхода луча из прямоугольника (луч начинается внутри)</summary>
        private static Vector2 ExitPoint(Rect rect, Vector2 origin, Vector2 direction)
        {
            var t = float.MaxValue;
            if (direction.x > 0.0001f) t = Mathf.Min(t, (rect.xMax - origin.x) / direction.x);
            if (direction.x < -0.0001f) t = Mathf.Min(t, (rect.xMin - origin.x) / direction.x);
            if (direction.y > 0.0001f) t = Mathf.Min(t, (rect.yMax - origin.y) / direction.y);
            if (direction.y < -0.0001f) t = Mathf.Min(t, (rect.yMin - origin.y) / direction.y);
            return t == float.MaxValue ? origin : origin + direction * t;
        }

        private static Vector2 Midpoint(List<Vector2> points)
        {
            var total = 0f;
            for (var i = 1; i < points.Count; i++)
                total += Vector2.Distance(points[i - 1], points[i]);
            var half = total * 0.5f;
            for (var i = 1; i < points.Count; i++)
            {
                var length = Vector2.Distance(points[i - 1], points[i]);
                if (half <= length && length > 0f)
                    return Vector2.Lerp(points[i - 1], points[i], half / length);
                half -= length;
            }

            return points.Count > 0 ? points[0] : Vector2.zero;
        }

        /// <summary>Слой рёбер охватывает все узлы и петли — иначе его отсекло бы при прокрутке</summary>
        private void UpdateEdgeLayerBounds()
        {
            _edgeBounds = _nodeData.Count == 0 ? new Rect(0, 0, 1, 1) : GetNodesBounds();
            const float margin = 200f;
            _edgeBounds = Rect.MinMaxRect(_edgeBounds.xMin - margin, _edgeBounds.yMin - margin,
                _edgeBounds.xMax + margin, _edgeBounds.yMax + margin);
            _edgeLayer.style.left = _edgeBounds.x;
            _edgeLayer.style.top = _edgeBounds.y;
            _edgeLayer.style.width = _edgeBounds.width;
            _edgeLayer.style.height = _edgeBounds.height;
            _edgeLayer.MarkDirtyRepaint();
        }

        #endregion

        #region Отрисовка

        private void DrawEdges(MeshGenerationContext context)
        {
            var painter = context.painter2D;
            var origin = _edgeBounds.position;

            foreach (var edge in _edges)
            {
                var selected = _selection.IsEdge && _selection.NodeId == edge.From &&
                               _selection.Transition == edge.Transition;
                var color = Palette(selected ? DefaultColors.GraphSelected
                    : edge.Issue == IssueSeverity.Error ? DefaultColors.GraphError
                    : edge.Issue == IssueSeverity.Warning ? DefaultColors.GraphWarning
                    : edge.WaitNext ? DefaultColors.GraphEdgeNext
                    : DefaultColors.GraphEdge);
                DrawPolyline(painter, edge.Points, origin, color, selected ? 3f : 2f, edge.WaitNext);
                DrawArrow(painter, edge.Points, origin, color);
            }

            if (_connectFrom != null && _nodeData.TryGetValue(_connectFrom, out var from))
            {
                var start = PortPoint(from.Position);
                DrawPolyline(painter, new List<Vector2> { start, _connectPoint }, origin,
                    Palette(DefaultColors.GraphSelected), 2f, true);
            }
        }

        private static void DrawPolyline(Painter2D painter, List<Vector2> points, Vector2 origin, Color color,
            float width, bool dashed)
        {
            if (points.Count < 2)
                return;

            painter.strokeColor = color;
            painter.lineWidth = width;
            painter.lineCap = LineCap.Round;
            painter.BeginPath();

            if (!dashed)
            {
                painter.MoveTo(points[0] - origin);
                for (var i = 1; i < points.Count; i++)
                    painter.LineTo(points[i] - origin);
                painter.Stroke();
                return;
            }

            const float dash = 8f;
            const float gap = 5f;
            var drawing = true;
            var remaining = dash;
            painter.MoveTo(points[0] - origin);
            for (var i = 1; i < points.Count; i++)
            {
                var a = points[i - 1];
                var b = points[i];
                var length = Vector2.Distance(a, b);
                var position = 0f;
                while (length - position > remaining)
                {
                    position += remaining;
                    var p = Vector2.Lerp(a, b, position / length) - origin;
                    if (drawing)
                        painter.LineTo(p);
                    else
                        painter.MoveTo(p);
                    drawing = !drawing;
                    remaining = drawing ? dash : gap;
                }

                remaining -= length - position;
                if (drawing)
                    painter.LineTo(b - origin);
                else
                    painter.MoveTo(b - origin);
            }

            painter.Stroke();
        }

        private static void DrawArrow(Painter2D painter, List<Vector2> points, Vector2 origin, Color color)
        {
            if (points.Count < 2)
                return;
            var tip = points[^1];
            var direction = (tip - points[^2]).normalized;
            if (direction == Vector2.zero)
                return;
            var side = new Vector2(-direction.y, direction.x);
            painter.fillColor = color;
            painter.BeginPath();
            painter.MoveTo(tip - origin);
            painter.LineTo(tip - direction * 10f + side * 5f - origin);
            painter.LineTo(tip - direction * 10f - side * 5f - origin);
            painter.ClosePath();
            painter.Fill();
        }

        private void RefreshStyles()
        {
            foreach (var pair in _nodes)
            {
                var data = _nodeData[pair.Key];
                var selected = _selection.IsNode && _selection.NodeId == pair.Key;
                var border = Palette(selected ? DefaultColors.GraphSelected
                    : data.Issue == IssueSeverity.Error ? DefaultColors.GraphError
                    : data.Issue == IssueSeverity.Warning ? DefaultColors.GraphWarning
                    : data.IsStart ? DefaultColors.GraphStart
                    : DefaultColors.GraphNodeBorder);
                pair.Value.Paint(
                    Palette(pair.Key == _current ? DefaultColors.GraphNodeCurrentBg : DefaultColors.GraphNodeBg),
                    border, selected ? 3f : 2f);
            }

            _edgeLayer.MarkDirtyRepaint();
        }

        #endregion

        #region Ввод

        private Vector2 ContentPoint(Vector3 worldPosition) => _content.WorldToLocal(worldPosition);

        private void ApplyTransform()
        {
            _content.style.translate = new Translate(_pan.x, _pan.y);
            _content.style.scale = new Scale(new Vector3(_zoom, _zoom, 1f));
        }

        private void OnPointerDown(PointerDownEvent evt)
        {
            Focus();
            var point = ContentPoint(evt.position);
            var edge = HitEdge(point);

            if (evt.button == 2 || (evt.button == 0 && edge == null))
            {
                if (evt.button == 0)
                    Select(GraphSelection.None);
                _panning = true;
                _panStartMouse = evt.position;
                _panStartPan = _pan;
                this.CapturePointer(evt.pointerId);
                return;
            }

            if (evt.button == 0)
            {
                Select(new GraphSelection(edge.From, edge.Transition));
                return;
            }

            if (evt.button == 1)
            {
                var selection = edge == null ? GraphSelection.None : new GraphSelection(edge.From, edge.Transition);
                Select(selection);
                ContextRequested?.Invoke(selection, point);
            }
        }

        private void OnPointerMove(PointerMoveEvent evt)
        {
            if (!_panning)
                return;
            _pan = _panStartPan + (Vector2)evt.position - _panStartMouse;
            ApplyTransform();
        }

        private void OnPointerUp(PointerUpEvent evt)
        {
            if (!_panning)
                return;
            _panning = false;
            this.ReleasePointer(evt.pointerId);
        }

        private void OnWheel(WheelEvent evt)
        {
            var local = this.WorldToLocal(evt.mousePosition);
            var graphPoint = (local - _pan) / _zoom;
            _zoom = Mathf.Clamp(_zoom * (evt.delta.y > 0 ? 0.9f : 1.1f), MinZoom, MaxZoom);
            _pan = local - graphPoint * _zoom;
            ApplyTransform();
            evt.StopPropagation();
        }

        private void OnKeyDown(KeyDownEvent evt)
        {
            if (evt.keyCode != KeyCode.Delete && evt.keyCode != KeyCode.Backspace)
                return;
            if (_selection.IsEmpty)
                return;
            DeleteRequested?.Invoke();
            evt.StopPropagation();
        }

        private CanvasEdge HitEdge(Vector2 point)
        {
            var limit = EdgeHitDistance / _zoom;
            CanvasEdge best = null;
            var bestDistance = limit;
            foreach (var edge in _edges)
                for (var i = 1; i < edge.Points.Count; i++)
                {
                    var distance = DistanceToSegment(point, edge.Points[i - 1], edge.Points[i]);
                    if (distance >= bestDistance)
                        continue;
                    bestDistance = distance;
                    best = edge;
                }

            return best;
        }

        private static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            var t = ab.sqrMagnitude < 0.0001f ? 0f : Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            return Vector2.Distance(p, a + ab * t);
        }

        private string HitNode(Vector2 point)
        {
            foreach (var node in _nodeData.Values)
                if (new Rect(node.Position, new Vector2(NodeWidth, NodeHeight)).Contains(point))
                    return node.Id;
            return null;
        }

        private static Vector2 PortPoint(Vector2 nodePosition) =>
            nodePosition + new Vector2(NodeWidth, NodeHeight * 0.5f);

        #endregion

        #region Узел

        private void OnNodeDragged(string nodeId, Vector2 position)
        {
            _nodeData[nodeId].Position = position;
            _nodes[nodeId].style.left = position.x;
            _nodes[nodeId].style.top = position.y;
            RecalculateEdges();
        }

        private void OnNodeDropped(string nodeId) => NodeMoved?.Invoke(nodeId, _nodeData[nodeId].Position);

        private void OnConnectStart(string nodeId, Vector3 worldPosition)
        {
            _connectFrom = nodeId;
            _connectPoint = ContentPoint(worldPosition);
            _edgeLayer.MarkDirtyRepaint();
        }

        private void OnConnectMove(Vector3 worldPosition)
        {
            _connectPoint = ContentPoint(worldPosition);
            _edgeLayer.MarkDirtyRepaint();
        }

        private void OnConnectEnd(Vector3 worldPosition)
        {
            var from = _connectFrom;
            _connectFrom = null;
            _edgeLayer.MarkDirtyRepaint();
            var target = HitNode(ContentPoint(worldPosition));
            if (from != null && target != null)
                Connected?.Invoke(from, target);
        }

        /// <summary>Элемент узла: заголовок-метка, имя, «порт» для протягивания перехода</summary>
        private sealed class NodeElement : VisualElement
        {
            private readonly StateGraphCanvas _canvas;
            private readonly string _id;
            private bool _dragging;
            private bool _moved;
            private Vector2 _dragStartMouse;
            private Vector2 _dragStartPosition;

            public NodeElement(StateGraphCanvas canvas, CanvasNode data)
            {
                _canvas = canvas;
                _id = data.Id;

                style.position = Position.Absolute;
                style.left = data.Position.x;
                style.top = data.Position.y;
                style.width = NodeWidth;
                style.height = NodeHeight;
                style.justifyContent = Justify.Center;
                style.paddingLeft = 8;
                style.paddingRight = 12;
                SetRadius(this, 6f);
                SetBorderWidth(this, 2f);

                var tag = data.IsStart && data.IsEnd ? "START · END" : data.IsStart ? "START" : data.IsEnd ? "END" : "";
                if (tag.Length > 0)
                {
                    var tagLabel = new Label(tag) { pickingMode = PickingMode.Ignore };
                    tagLabel.style.fontSize = 9;
                    tagLabel.style.color = Palette(data.IsStart ? DefaultColors.GraphStart : DefaultColors.GraphNodeTagText);
                    Add(tagLabel);
                }

                var nameLabel = new Label(data.Label) { pickingMode = PickingMode.Ignore };
                nameLabel.style.color = Palette(DefaultColors.GraphNodeText);
                nameLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
                nameLabel.style.overflow = Overflow.Hidden;
                nameLabel.style.textOverflow = TextOverflow.Ellipsis;
                nameLabel.style.whiteSpace = WhiteSpace.NoWrap;
                Add(nameLabel);

                var port = new VisualElement();
                port.style.position = Position.Absolute;
                port.style.right = -7;
                port.style.top = NodeHeight * 0.5f - 7f - 2f;
                port.style.width = 14;
                port.style.height = 14;
                port.style.backgroundColor = Palette(DefaultColors.GraphPort);
                SetRadius(port, 7f);
                port.tooltip = "Drag to another node to add a transition";
                port.RegisterCallback<PointerDownEvent>(OnPortDown);
                port.RegisterCallback<PointerMoveEvent>(OnPortMove);
                port.RegisterCallback<PointerUpEvent>(OnPortUp);
                Add(port);

                RegisterCallback<PointerDownEvent>(OnDown);
                RegisterCallback<PointerMoveEvent>(OnMove);
                RegisterCallback<PointerUpEvent>(OnUp);
            }

            public void Paint(Color background, Color border, float borderWidth)
            {
                SetBorderWidth(this, borderWidth);
                style.backgroundColor = background;
                style.borderTopColor = border;
                style.borderBottomColor = border;
                style.borderLeftColor = border;
                style.borderRightColor = border;
            }

            private void OnDown(PointerDownEvent evt)
            {
                if (evt.button == 2)
                    return; // СКМ — сдвиг полотна

                _canvas.Focus();
                var selection = new GraphSelection(_id, -1);
                _canvas.Select(selection);
                evt.StopPropagation();

                if (evt.button == 1)
                {
                    _canvas.ContextRequested?.Invoke(selection, _canvas.ContentPoint(evt.position));
                    return;
                }

                if (evt.button != 0)
                    return;
                _dragging = true;
                _moved = false;
                _dragStartMouse = evt.position;
                _dragStartPosition = _canvas._nodeData[_id].Position;
                this.CapturePointer(evt.pointerId);
            }

            private void OnMove(PointerMoveEvent evt)
            {
                if (!_dragging)
                    return;
                var delta = ((Vector2)evt.position - _dragStartMouse) / _canvas._zoom;
                if (!_moved && delta.sqrMagnitude < 4f)
                    return;
                _moved = true;
                _canvas.OnNodeDragged(_id, _dragStartPosition + delta);
                evt.StopPropagation();
            }

            private void OnUp(PointerUpEvent evt)
            {
                if (!_dragging)
                    return;
                _dragging = false;
                this.ReleasePointer(evt.pointerId);
                if (_moved)
                    _canvas.OnNodeDropped(_id);
                evt.StopPropagation();
            }

            private void OnPortDown(PointerDownEvent evt)
            {
                if (evt.button != 0)
                    return;
                ((VisualElement)evt.currentTarget).CapturePointer(evt.pointerId);
                _canvas.OnConnectStart(_id, evt.position);
                evt.StopPropagation();
            }

            private void OnPortMove(PointerMoveEvent evt)
            {
                var port = (VisualElement)evt.currentTarget;
                if (!port.HasPointerCapture(evt.pointerId))
                    return;
                _canvas.OnConnectMove(evt.position);
                evt.StopPropagation();
            }

            private void OnPortUp(PointerUpEvent evt)
            {
                var port = (VisualElement)evt.currentTarget;
                if (!port.HasPointerCapture(evt.pointerId))
                    return;
                port.ReleasePointer(evt.pointerId);
                _canvas.OnConnectEnd(evt.position);
                evt.StopPropagation();
            }

            private static void SetRadius(VisualElement element, float radius)
            {
                element.style.borderTopLeftRadius = radius;
                element.style.borderTopRightRadius = radius;
                element.style.borderBottomLeftRadius = radius;
                element.style.borderBottomRightRadius = radius;
            }

            private static void SetBorderWidth(VisualElement element, float width)
            {
                element.style.borderTopWidth = width;
                element.style.borderBottomWidth = width;
                element.style.borderLeftWidth = width;
                element.style.borderRightWidth = width;
            }
        }

        #endregion
    }
}
