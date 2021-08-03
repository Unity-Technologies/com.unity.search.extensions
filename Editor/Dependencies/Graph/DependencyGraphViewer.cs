using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Random = UnityEngine.Random;

namespace UnityEditor.Search
{
    class DependencyGraphViewer : EditorWindow
    {
        private const float kInitialPosOffset = -0;
        private const float kNodeSize = 100.0f;
        private const float kHalfNodeSize = kNodeSize / 2.0f;
        private const float kPreviewSize = 64.0f;

        private readonly Color kWeakInColor = new Color(240f / 255f, 240f / 255f, 240f / 255f);
        private readonly Color kWeakOutColor = new Color(120 / 255f, 134f / 255f, 150f / 255f);
        private readonly Color kDirectInColor = new Color(146 / 255f, 196 / 255f, 109 / 255f);
        private readonly Color kDirectOutColor = new Color(83 / 255f, 150 / 255f, 153 / 255f);

        private Graph graph;
        private DependencyDatabase db;
        private IGraphLayout graphLayout;

        private Node selecteNode = null;
        private string status = "";
        private float zoom = 1.0f;
        private Vector2 pan = new Vector2(kInitialPosOffset, kInitialPosOffset);

        private Rect graphRect => new Rect(0, 0, position.width, position.height);

        internal void OnEnable()
        {
            titleContent = new GUIContent("Dependency Graph", Icons.dependencies);
            db = new DependencyDatabase();
            graph = new Graph(db) { nodeInitialPositionCallback = GetNodeInitialPosition };
        }

        private void Import(ICollection<UnityEngine.Object> objects)
        {
            Add(objects, ViewCenter());
            if (graphLayout == null)
                SetLayout(new OrganicLayout());
        }

        private void Add(ICollection<UnityEngine.Object> objects, in Vector2 pos)
        {
            var npos = pos;
            int gridPositionIndex = 0;
            var gridSquareSize = Mathf.RoundToInt(Mathf.Sqrt(objects.Count));
            foreach (var e in objects)
            {
                var path = AssetDatabase.GetAssetPath(e);
                if (string.IsNullOrEmpty(path))
                    continue;

                int depID = db.FindResourceByName(path);
                if (depID < 0)
                    continue;
                var n = graph.FindNode(depID);
                if (n == null)
                {
                    n = graph.Add(depID, pos);
                    n.pinned = true;
                    n.rect.position = npos;
                    npos.x += n.rect.size.x * 1.5f;
                    gridPositionIndex++;

                    if (gridPositionIndex > gridSquareSize)
                    {
                        npos.x = pos.x;
                        npos.y += n.rect.size.y * 1.5f;
                        gridPositionIndex = 0;
                    }
                }

                var deps = db.GetResourceDependencies(depID).Where(did => graph.FindNode(did) != null);
                graph.AddNodes(n, deps.ToArray(), LinkType.DirectIn, null);

                var refs = db.GetResourceReferences(depID).Where(did => graph.FindNode(did) != null);
                graph.AddNodes(n, refs.ToArray(), LinkType.DirectOut, null);
            }

            if (graphLayout == null)
                SetLayout(new OrganicLayout());
        }

        internal void OnGUI()
        {
            if (db == null || graph == null || graph.nodes == null)
                return;

            var evt = Event.current;
            DrawView(evt);
            DrawHUD(evt);
            HandleEvent(evt);
        }

        private Rect GetNodeInitialPosition(Graph graphModel, Vector2 offset)
        {
            return new Rect(
                offset.x + Random.Range(-graphRect.width / 2, graphRect.width / 2),
                offset.y + Random.Range(-graphRect.height / 2, graphRect.height / 2),
                kNodeSize, kNodeSize);
        }

        Vector2 LocalToView(in Vector2 localPosition)
        {
            return localPosition * 1f / zoom - pan;
        }

        Vector2 ViewCenter()
        {
            return LocalToView(graphRect.center);
        }

        private void HandleEvent(Event e)
        {
            if (e.type == EventType.MouseDrag)
            {
                pan.x += e.delta.x / zoom;
                pan.y += e.delta.y / zoom;
                e.Use();
            }
            else if (e.type == EventType.MouseDown)
            {
                Debug.Log($"P={LocalToView(e.mousePosition)}, C={ViewCenter()}");
            }
            else if (e.type == EventType.ScrollWheel)
            {
                var zoomDelta = 0.1f;
                float delta = e.delta.x + e.delta.y;
                zoomDelta = delta < 0 ? zoomDelta : -zoomDelta;

                float oldZoom = zoom;
                zoom = Mathf.Clamp(zoom + zoomDelta, 0.2f, 6.25f);

                if (zoomDelta > 0)
                    pan += (graphRect.center - e.mousePosition) * zoom;

                e.Use();
            }
            else if (selecteNode != null && e.type == EventType.KeyDown && e.keyCode == KeyCode.Delete)
            {
                graph.edges.RemoveAll(e => e.Source == selecteNode || e.Target == selecteNode);
                graph.nodes.Remove(selecteNode);
                selecteNode = null;
                e.Use();
            }
            else if (e.type == EventType.DragUpdated)
            {
                if (IsDragTargetValid()) 
                    DragAndDrop.visualMode = DragAndDropVisualMode.Link;
                else 
                    DragAndDrop.visualMode = DragAndDropVisualMode.Rejected;
                e.Use();
            }
            else if (e.type == EventType.DragPerform)
            {
                HandleDragPerform(e);
                e.Use();
            }
        }

        private void HandleDragPerform(in Event e)
        {
            Add(DragAndDrop.objectReferences, LocalToView(e.mousePosition));
        }

        private bool IsDragTargetValid()
        {
            return DragAndDrop.objectReferences.Any(o => AssetDatabase.GetAssetPath(o) != null);
        }

        private Color GetLinkColor(in LinkType linkType)
        {
            switch (linkType)
            {
                case LinkType.Self:
                    return Color.red;
                case LinkType.WeakIn:
                    return kWeakInColor;
                case LinkType.WeakOut:
                    return kWeakOutColor;
                case LinkType.DirectIn:
                    return kDirectInColor;
                case LinkType.DirectOut:
                    return kDirectOutColor;
            }

            return Color.red;
        }

        private void DrawEdge(in Rect viewportRect, in Edge edge, in Vector2 from, in Vector2 to)
        {
            if (edge.hidden)
                return;
            var edgeScale = to - from;
            var edgeBounds = new Rect(
                Mathf.Min(from.x, to.x) - pan.x, Mathf.Min(from.y, to.y) - pan.y,
                Mathf.Abs(edgeScale.x), Mathf.Abs(edgeScale.y));
            if (!edgeBounds.Overlaps(viewportRect))
                return;
            var edgeColor = GetLinkColor(edge.linkType);
            bool selected = selecteNode == edge.Source || selecteNode == edge.Target;
            if (selected)
            {
                const float kHightlightFactor = 1.65f;
                edgeColor.r = Math.Min(edgeColor.r * kHightlightFactor, 1.0f);
                edgeColor.g = Math.Min(edgeColor.g * kHightlightFactor, 1.0f);
                edgeColor.b = Math.Min(edgeColor.b * kHightlightFactor, 1.0f);
            }
            Handles.DrawBezier(from, to,
                new Vector2(edge.Source.rect.xMax + kHalfNodeSize, edge.Source.rect.center.y) + pan,
                new Vector2(edge.Target.rect.xMin - kHalfNodeSize, edge.Target.rect.center.y) + pan,
                edgeColor, null, 5f);
        }

        protected void DrawNode(Event evt, in Rect viewportRect, Node node)
        {
            var windowRect = new Rect(node.rect.position + pan, node.rect.size);
            if (!node.rect.Overlaps(viewportRect))
                return;

            node.rect = GUI.Window(node.index, windowRect, _ => DrawNodeWindow(windowRect, evt, node), node.title);

            if (node.rect.Contains(evt.mousePosition))
            {
                if (string.IsNullOrEmpty(status))
                    Repaint();
                status = node.tooltip;
            }

            node.rect.x -= pan.x;
            node.rect.y -= pan.y;
        }

        private void DrawNodeWindow(in Rect windowRect, Event evt, in Node node)
        {
            const float kPreviewOffsetY = 10.0f;
            if (evt.type == EventType.Repaint && node.preview)
            {
                var previewOffset = (kNodeSize - kPreviewSize) / 2.0f;
                GUI.DrawTexture(new Rect(
                        previewOffset, previewOffset + kPreviewOffsetY,
                        kPreviewSize, kPreviewSize), node.preview);
            }

            const float kHeightDiff = 2.0f;
            const float kButtonWidth = 16.0f, kButtonHeight = 18f;
            const float kRightPadding = 17.0f, kBottomPadding = kRightPadding - kHeightDiff;
            var buttonRect = new Rect(windowRect.width - kRightPadding, windowRect.height - kBottomPadding - 4f, kButtonWidth, kButtonHeight);
            if (!node.expanded && GUI.Button(buttonRect, "+"))
            {
                graph.ExpandNode(node);
                graphLayout.Calculate(graph, 0.05f);
            }

            buttonRect = new Rect(windowRect.width - kRightPadding, kBottomPadding, 23, 26);
            node.pinned = EditorGUI.Toggle(buttonRect, node.pinned);

            if (evt.type == EventType.MouseDown)
            {
                if (evt.button == 0)
                {
                    var selectedObject = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(node.name);
                    if (evt.clickCount == 1 && selectedObject)
                    {
                        selecteNode = node;
                        EditorGUIUtility.PingObject(selectedObject.GetInstanceID());
                    }
                    else if (evt.clickCount == 2)
                    {
                        Selection.activeObject = selectedObject;
                        evt.Use();
                    }
                }
                else if (evt.button == 2)
                {
                    node.pinned = !node.pinned;
                    evt.Use();
                }
            }            
            GUI.DragWindow();
        }

        private void DrawGraph(Event evt)
        {
            if (graphLayout?.Animated ?? false)
            {
                if (graphLayout.Calculate(graph, 0.05f))
                    Repaint();
            }

            var viewportRect = new Rect(-pan.x, -pan.y, graphRect.width, graphRect.height).ScaleSizeBy(1f / zoom, -pan);
            if (evt.type == EventType.Layout)
            {
                // Reset status message, it will be set again when hovering a node.
                status = "";
            }
            else if (evt.type == EventType.Repaint)
            {
                Handles.BeginGUI();
                foreach (var edge in graph.edges)
                    DrawEdge(viewportRect, edge, edge.Source.rect.center + pan, edge.Target.rect.center + pan);
                Handles.EndGUI();
            }

            BeginWindows();
            foreach (var node in graph.nodes)
                DrawNode(evt, viewportRect, node);
            EndWindows();
        }

        private void DrawView(Event evt)
        {
            EditorZoomArea.Begin(zoom, graphRect);
            DrawGraph(evt);
            EditorZoomArea.End();
        }

        private void DrawHUD(Event evt)
        {
            if (!string.IsNullOrEmpty(status))
                GUI.Label(new Rect(4, graphRect.yMax - 20, graphRect.width, 20), status);

            if (evt.type == EventType.MouseDown && evt.button == 1)
            {
                var menu = new GenericMenu();
                menu.AddItem(new GUIContent("Import"), false, () => Import(Selection.objects));
                menu.AddItem(new GUIContent("Clear"), false, () => ClearGraph());
                menu.AddSeparator("");

                menu.AddItem(new GUIContent("Relayout"), false, () => Relayout());
                menu.AddItem(new GUIContent("Layout/Springs"), false, () => SetLayout(new ForceDirectedLayout(graph)));
                menu.AddItem(new GUIContent("Layout/Organic"), false, () => SetLayout(new OrganicLayout()));

                menu.ShowAsContext();
                evt.Use();
            }
        }

        private void ClearGraph()
        {
            graph.Clear();
        }

        private void Relayout()
        {
// 			foreach (var v in graph.nodes)
// 				v.pinned = false;
            SetLayout(graphLayout);
        }

        void SetLayout(IGraphLayout layout)
        {
            graphLayout = layout;
            graphLayout.Calculate(graph, 0.05f);
            if (graph.nodes.Count > 0)
                pan = -graph.nodes[0].rect.center + graphRect.center;
            Repaint();
        }

        [MenuItem("Window/Search/Dependency Graph Viewer", priority = 5680)]
        internal static void OpenNew()
        {
            var win = CreateWindow<DependencyGraphViewer>();
            win.position = Utils.GetMainWindowCenteredPosition(new Vector2(800, 500));
            win.Show();
        }
    }
}
