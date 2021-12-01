#if !USE_SEARCH_DEPENDENCY_VIEWER || USE_SEARCH_MODULE
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.ShortcutManagement;
using UnityEngine;
using Random = UnityEngine.Random;

namespace UnityEditor.Search
{
    class DependencyGraphViewer : EditorWindow
    {
        private const float kInitialPosOffset = -0;
        private const float kNodeWidth = 180.0f;
        private const float kNodeHeight = 100.0f;
        private const float kNodeHeaderRatio = 0.55f;
        private const float kNodeHeaderHeight = kNodeHeight * kNodeHeaderRatio;
        private const float kHalfNodeWidth = kNodeWidth / 2.0f;
        private const float kBaseZoomMinLevel = 0.2f;
        private const float kStatusBarHeight = 20f;

        static class Colors
        {
            static readonly Color k_NodeHeaderDark = new Color(38f / 255f, 38f / 255f, 38f / 255f);
            static readonly Color k_NodeDescription = new Color(79 / 255f, 79 / 255f, 79 / 255f);

            public static Color nodeHeader => EditorGUIUtility.isProSkin ? k_NodeHeaderDark : Color.blue;
            public static Color nodeDescription => EditorGUIUtility.isProSkin ? k_NodeDescription : Color.white;
        }

        private readonly Color kWeakInColor = new Color(240f / 255f, 240f / 255f, 240f / 255f);
        private readonly Color kWeakOutColor = new Color(120 / 255f, 134f / 255f, 150f / 255f);
        private readonly Color kDirectInColor = new Color(146 / 255f, 196 / 255f, 109 / 255f);
        private readonly Color kDirectOutColor = new Color(83 / 255f, 150 / 255f, 153 / 255f);

        private Graph graph;
        private DependencyDatabase db;
        private IGraphLayout graphLayout;

        private Node selectedNode = null;
        private string status = "";
        private float zoom = 1.0f;
        private float minZoom = kBaseZoomMinLevel;
        private Vector2 pan = new Vector2(kInitialPosOffset, kInitialPosOffset);
        private bool showStatus = false;

        private Rect graphRect => new Rect(0, 0, rootVisualElement.worldBound.width, rootVisualElement.worldBound.height - (showStatus ? kStatusBarHeight : 0f));
        private Rect statusBarRect => new Rect(0, graphRect.yMax, graphRect.width, (showStatus ? kStatusBarHeight : 0f));

        const string k_FrameAllShortcutName = "Search/Dependency Graph Viewer/Frame All";
        const string k_ShowStatusBarShortcutName = "Search/Dependency Graph Viewer/Show Status Bar";

        GUIStyle m_StatusBarStyle;
        GUIStyle m_NodeDescriptionStyle;
        GUIStyleState m_NodeDescriptionStyleState;

        internal void OnEnable()
        {
            titleContent = new GUIContent("Dependency Graph", Icons.quicksearch);
            db = new DependencyDatabase();
            graph = new Graph(db) { nodeInitialPositionCallback = GetNodeInitialPosition };

            m_StatusBarStyle = new GUIStyle()
            {
                name = "quick-search-status-bar-background",
                fixedHeight = kStatusBarHeight
            };

            m_NodeDescriptionStyleState = new GUIStyleState()
            {
                background = null,
                scaledBackgrounds = new Texture2D[] { null },
                textColor = Colors.nodeDescription
            };
            m_NodeDescriptionStyle = new GUIStyle()
            {
                name = "Label",
                fontSize = 8,
                normal = m_NodeDescriptionStyleState,
                hover = m_NodeDescriptionStyleState,
                active = m_NodeDescriptionStyleState,
                focused = m_NodeDescriptionStyleState,
                padding = new RectOffset(3, 2, 1, 1)
            };
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
            else
                ComputeNewMinZoomLevel();
        }

        internal void OnGUI()
        {
            if (db == null || graph == null || graph.nodes == null)
                return;

            var evt = Event.current;
            DrawView(evt);
            DrawHUD(evt);
            DrawStatusBar();
            HandleEvent(evt);
        }

        private Rect GetNodeInitialPosition(Graph graphModel, Vector2 offset)
        {
            return new Rect(
                offset.x + Random.Range(-graphRect.width / 2, graphRect.width / 2),
                offset.y + Random.Range(-graphRect.height / 2, graphRect.height / 2),
                kNodeWidth, kNodeHeight);
        }

        Vector2 LocalToView(in Vector2 localPosition)
        {
            return localPosition * 1f / zoom - pan;
        }

        Rect LocalToView(in Rect localRect)
        {
            return new Rect(LocalToView(localRect.position), localRect.size / zoom);
        }

        Vector2 ViewCenter()
        {
            return LocalToView(graphRect.center);
        }

        private void HandleEvent(Event e)
        {
            if (e.type == EventType.MouseDrag && graphRect.Contains(e.mousePosition))
            {
                pan.x += e.delta.x / zoom;
                pan.y += e.delta.y / zoom;
                e.Use();
            }
            else if (e.type == EventType.ScrollWheel && graphRect.Contains(e.mousePosition))
            {
                var zoomDelta = 0.1f;
                float delta = e.delta.x + e.delta.y;
                zoomDelta = delta < 0 ? zoomDelta : -zoomDelta;

                // To make the zoom focus on the target point, you have to make sure that this
                // point stays the same (in local space) after the transformations.
                // To do this, you can solve a little linear algebra system.
                // Let:
                // - TPLi be the initial target point (local space)
                // - TPLf be the final target point (local space)
                // - TPV be the target point (view space/global space)
                // - P1 the pan before the transformation
                // - P2 the pan after the transformation
                // - Z1 the zoom level before the transformation
                // - Z2 the zoom level after the transformation
                // Solve this system:
                // Eq1: TPV = TPLi/Z1 - P1
                // Eq2: Z2 = Z1 + delta
                // Eq3: TPLf = (TPV + P2) * Z2
                // We know that at the end, TPLf == TPLi, delta is a constant that we know,
                // so we only need to find P2. By substituting Eq1 and Eq2 into Eq3, we get
                // TPLf = (TPLi/Z1 - P1 + P2) * Z2
                // 0 = TPLi*delta/Z1 - P1*Z2 + P2*Z2
                // P2 = P1 - TPLi*delta/(Z1*Z2)
                float oldZoom = zoom;
                var targetLocal = e.mousePosition;
                SetZoom(zoom + zoomDelta);
                var realDelta = zoom - oldZoom;
                pan -= (targetLocal * realDelta / (oldZoom * zoom));

                e.Use();
            }
            else if (selectedNode != null && e.type == EventType.KeyDown && e.keyCode == KeyCode.Delete)
            {
                graph.edges.RemoveAll(e => e.Source == selectedNode || e.Target == selectedNode);
                graph.nodes.Remove(selectedNode);
                selectedNode = null;
                e.Use();
            }
            else if (selectedNode != null && e.type == EventType.MouseDown)
            {
                selectedNode = null;
                e.Use();
            }
            else if (e.type == EventType.DragUpdated && graphRect.Contains(e.mousePosition))
            {
                if (IsDragTargetValid())
                    DragAndDrop.visualMode = DragAndDropVisualMode.Link;
                else
                    DragAndDrop.visualMode = DragAndDropVisualMode.Rejected;
                e.Use();
            }
            else if (e.type == EventType.DragPerform && graphRect.Contains(e.mousePosition))
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
            bool selected = selectedNode == edge.Source || selectedNode == edge.Target;
            if (selected)
            {
                const float kHightlightFactor = 1.65f;
                edgeColor.r = Math.Min(edgeColor.r * kHightlightFactor, 1.0f);
                edgeColor.g = Math.Min(edgeColor.g * kHightlightFactor, 1.0f);
                edgeColor.b = Math.Min(edgeColor.b * kHightlightFactor, 1.0f);
            }
            Handles.DrawBezier(from, to,
                new Vector2(edge.Source.rect.xMax + kHalfNodeWidth, edge.Source.rect.center.y) + pan,
                new Vector2(edge.Target.rect.xMin - kHalfNodeWidth, edge.Target.rect.center.y) + pan,
                edgeColor, null, 5f);
        }

        protected void DrawNode(Event evt, in Rect viewportRect, Node node)
        {
            var windowRect = new Rect(node.rect.position + pan, node.rect.size);
            if (!node.rect.Overlaps(viewportRect))
                return;

            node.rect = GUI.Window(node.index, windowRect, _ => DrawNodeWindow(windowRect, evt, node), string.Empty);
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
            const float borderRadius = 2.0f;
            const float borderWidth = 0f;

            var nodeRect = new Rect(0, 0, windowRect.width, windowRect.height).PadBy(borderWidth);

            // Header
            var headerRect = new Rect(nodeRect.x, nodeRect.y, nodeRect.width, kNodeHeaderHeight);
            var borderRadius2 = new Vector4(borderRadius, borderRadius, 0, 0);
            GUI.DrawTexture(headerRect, EditorGUIUtility.whiteTexture, ScaleMode.StretchToFill, false, 0f, Colors.nodeHeader, Vector4.zero, borderRadius2);

            // Preview
            const float nodeMargin = 10.0f;
            var hasPreview = node.preview != null;
            var previewSize = kNodeHeaderHeight - 2 * nodeMargin;
            if (evt.type == EventType.Repaint && hasPreview)
            {
                GUI.DrawTexture(new Rect(
                    headerRect.x + nodeMargin, headerRect.y + nodeMargin,
                    previewSize, previewSize), node.preview);
            }

            // Title
            var titleHeight = 20f;
            var titleOffsetX = nodeMargin;
            var titleWidth = headerRect.width - 2 * nodeMargin;
            if (hasPreview)
            {
                titleOffsetX += previewSize + nodeMargin;
                titleWidth = titleWidth - previewSize - nodeMargin;
            }
            var nodeTitleRect = new Rect(titleOffsetX, nodeMargin, titleWidth, titleHeight);
            GUI.Label(nodeTitleRect, node.title);

            // Description
            var descriptionHeight = 16f;
            var descriptionOffsetX = nodeMargin;
            var descriptionWidth = headerRect.width - 2 * nodeMargin;
            if (hasPreview)
            {
                descriptionOffsetX += previewSize + nodeMargin;
                descriptionWidth = titleWidth - previewSize - nodeMargin;
            }
            var nodeDescriptionRect = new Rect(descriptionOffsetX, nodeTitleRect.yMax, descriptionWidth, descriptionHeight);
            GUI.Label(nodeDescriptionRect, "In Project", m_NodeDescriptionStyle);

            // Expand Dependencies
            const float expandButtonHeight = 20f;
            var buttonStyle = GUI.skin.button;
            var expandDependenciesContent = new GUIContent($"{node.dependencyCount}");
            var buttonContentSize = buttonStyle.CalcSize(expandDependenciesContent);
            var buttonRect = new Rect(nodeRect.width - nodeMargin - buttonContentSize.x, nodeRect.height - nodeMargin - expandButtonHeight, buttonContentSize.x, expandButtonHeight);
            if (GUI.Button(buttonRect, expandDependenciesContent))
            {
                graph.ExpandNodeDependencies(node);
                graphLayout.Calculate(new GraphLayoutParameters { graph = graph, deltaTime = 0.05f, expandedNode = node });
                ComputeNewMinZoomLevel();
            }

            // Expand References
            var expandReferencesContent = new GUIContent($"{node.referenceCount}");
            buttonContentSize = buttonStyle.CalcSize(expandReferencesContent);
            buttonRect = new Rect(nodeRect.x + nodeMargin, nodeRect.height - nodeMargin - expandButtonHeight, buttonContentSize.x, expandButtonHeight);
            if (GUI.Button(buttonRect, expandReferencesContent))
            {
                graph.ExpandNodeReferences(node);
                graphLayout.Calculate(new GraphLayoutParameters { graph = graph, deltaTime = 0.05f, expandedNode = node });
                ComputeNewMinZoomLevel();
            }

            // Pin
            buttonRect = new Rect(nodeRect.width - borderWidth * 2 - 16, borderWidth * 2, 16, 16);
            node.pinned = EditorGUI.Toggle(buttonRect, node.pinned);

            // Handle events
            if (evt.type == EventType.MouseDown && nodeRect.Contains(evt.mousePosition))
            {
                if (evt.button == 0)
                {
                    var selectedObject = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(node.name);
                    if (evt.clickCount == 1 && selectedObject)
                    {
                        selectedNode = node;
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
                if (graphLayout.Calculate(new GraphLayoutParameters { graph = graph, deltaTime = 0.05f }))
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
            var worldBoundRect = this.rootVisualElement.worldBound;
            EditorZoomArea.Begin(zoom, graphRect, worldBoundRect);
            DrawGraph(evt);
            EditorZoomArea.End();
        }

        private void DrawHUD(Event evt)
        {
            if (evt.type == EventType.MouseDown && evt.button == 1)
            {
                var menu = new GenericMenu();
                menu.AddItem(new GUIContent("Import"), false, () => Import(Selection.objects));
                menu.AddItem(new GUIContent("Clear"), false, () => ClearGraph());
                menu.AddSeparator("");

                menu.AddItem(new GUIContent("Frame All"), false, () => FrameAll());
                menu.AddItem(new GUIContent("Relayout"), false, () => Relayout());
                menu.AddItem(new GUIContent("Layout/Springs"), false, () => SetLayout(new ForceDirectedLayout(graph)));
                menu.AddItem(new GUIContent("Layout/Organic"), false, () => SetLayout(new OrganicLayout()));
                menu.AddItem(new GUIContent("Layout/Column"), false, () => SetLayout(new DependencyColumnLayout()));

                menu.ShowAsContext();
                evt.Use();
            }
        }

        void DrawStatusBar()
        {
            if (!showStatus)
                return;

            GUI.Box(statusBarRect, GUIContent.none, m_StatusBarStyle);

            if (!string.IsNullOrEmpty(status))
                GUI.Label(new Rect(4, statusBarRect.yMin, statusBarRect.width, statusBarRect.height), status);

            if (graph == null || graph.nodes == null || graph.edges == null)
                return;

            var graphInfoText = $"Nodes: {graph.nodes.Count}   Edges: {graph.edges.Count} ({graph.edges.Count(e => e.linkType.IsWeakLink())} weak links)";
            var labelStyle = GUI.skin.label;
            var textSize = labelStyle.CalcSize(new GUIContent(graphInfoText));
            GUI.Label(new Rect(statusBarRect.xMax - textSize.x, statusBarRect.yMin, textSize.x, statusBarRect.height), graphInfoText);
        }

        private void ClearGraph()
        {
            graph.Clear();
            SetMinZoomLevel(kBaseZoomMinLevel);
        }

        private void Relayout()
        {
            SetLayout(graphLayout);
            FrameAll();
        }

        void SetLayout(IGraphLayout layout)
        {
            graphLayout = layout;
            graphLayout?.Calculate(new GraphLayoutParameters {graph = graph, deltaTime = 0.05f});
            if (graph.nodes.Count > 0)
                Center(graph.nodes[0]);
            ComputeNewMinZoomLevel();
            Repaint();
        }

        [Shortcut(k_FrameAllShortcutName, typeof(DependencyGraphViewer), KeyCode.F)]
        static void FrameAll(ShortcutArguments args)
        {
            var window = args.context as DependencyGraphViewer;
            if (window == null)
                return;
            window.FrameAll();
        }

        void FrameAll()
        {
            if (graph?.nodes == null || graph.nodes.Count == 0)
                return;

            var bb = DependencyGraphUtils.GetBoundingBox(graph.nodes);
            FrameRegion(bb);
        }

        void FrameRegion(Rect region)
        {
            var newZoomLevel = GetZoomLevelForRegion(region);
            SetMinZoomLevel(newZoomLevel);
            SetZoom(newZoomLevel);
            Center(region.center);
            Repaint();
        }

        void Center(in Vector2 target)
        {
            pan = graphRect.center / zoom - target;
        }

        void Center(in Rect target)
        {
            Center(target.center);
        }

        void Center(Node node)
        {
            Center(node.rect.center);
        }

        static float GetRatio(Rect region)
        {
            return region.width / region.height;
        }

        void SetZoom(float targetZoom)
        {
            zoom = Mathf.Clamp(targetZoom, minZoom, 6.25f);
        }

        float GetZoomLevelForRegion(Rect region)
        {
            if (region.width == 0 || region.height == 0)
                return 1f;
            var currentRegion = LocalToView(graphRect);
            var currentRatio = GetRatio(currentRegion);
            var newRegionRatio = GetRatio(region);
            var newRegionCenter = region.center;

            if (currentRatio > newRegionRatio)
                region.width = region.height * currentRatio;
            else
                region.height = region.width / currentRatio;

            region.center = newRegionCenter;

            var newZoomLevel = graphRect.width / region.width;
            return newZoomLevel;
        }

        void SetMinZoomLevel(float newMinZoom)
        {
            minZoom = Mathf.Min(kBaseZoomMinLevel, newMinZoom);
        }

        void ComputeNewMinZoomLevel()
        {
            var bb = DependencyGraphUtils.GetBoundingBox(graph.nodes);
            var minZoomLevel = GetZoomLevelForRegion(bb);
            SetMinZoomLevel(minZoomLevel);
        }

        [Shortcut(k_ShowStatusBarShortcutName, typeof(DependencyGraphViewer), KeyCode.F1)]
        internal static void ShowStatusBar(ShortcutArguments args)
        {
            var window = args.context as DependencyGraphViewer;
            if (window == null)
                return;
            window.ToggleShowStatusBar();
        }

        void ToggleShowStatusBar()
        {
            showStatus = !showStatus;
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
#endif
