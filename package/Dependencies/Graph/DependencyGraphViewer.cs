using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.ShortcutManagement;
using UnityEngine;
using Random = UnityEngine.Random;

namespace UnityEditor.Search
{
    enum EdgeDisplay
    {
        Bezier,
        Elbow,
    }

    class DependencyGraphViewer : EditorWindow
    {
        const float kInitialPosOffset = -0;
        const float kNodeWidth = 180.0f;
        const float kNodeHeight = 100.0f;
        const float kNodeHeaderRatio = 0.55f;
        const float kNodeHeaderHeight = kNodeHeight * kNodeHeaderRatio;
        const float kHalfNodeWidth = kNodeWidth / 2.0f;
        const float kBaseZoomMinLevel = 0.2f;
        const float kStatusBarHeight = 20f;
        const float kNodeMargin = 10.0f;
        const float kBorderRadius = 2.0f;
        const float kBorderWidth = 0f;
        const float kExpandButtonHeight = 20f;
        const float kElbowCornerRadius = 10f;

        static class Colors
        {
            static readonly Color k_NodeHeaderDark = new Color(38f / 255f, 38f / 255f, 38f / 255f);
            static readonly Color k_NodeDescription = new Color(79 / 255f, 79 / 255f, 79 / 255f);

            public static Color nodeHeader => EditorGUIUtility.isProSkin ? k_NodeHeaderDark : Color.blue;
            public static Color nodeDescription => EditorGUIUtility.isProSkin ? k_NodeDescription : Color.white;
        }

        static readonly Color kWeakInColor = new Color(240f / 255f, 240f / 255f, 240f / 255f);
        static readonly Color kWeakOutColor = new Color(120 / 255f, 134f / 255f, 150f / 255f);
        static readonly Color kDirectInColor = new Color(146 / 255f, 196 / 255f, 109 / 255f);
        static readonly Color kDirectOutColor = new Color(83 / 255f, 150 / 255f, 153 / 255f);

        Graph graph;
        DependencyDatabase db;
        IGraphLayout graphLayout;

        Node selectedNode = null;
        string status = "";
        float zoom = 1.0f;
        float minZoom = kBaseZoomMinLevel;
        Vector2 pan = new Vector2(kInitialPosOffset, kInitialPosOffset);
        bool showStatus = false;
        EdgeDisplay currentEdgeDisplay = EdgeDisplay.Bezier;

        Rect graphRect => new Rect(0, 0, rootVisualElement.worldBound.width, rootVisualElement.worldBound.height - (showStatus ? kStatusBarHeight : 0f));
        Rect statusBarRect => new Rect(0, graphRect.yMax, graphRect.width, (showStatus ? kStatusBarHeight : 0f));

        const string k_FrameAllShortcutName = "Search/Dependency Graph Viewer/Frame All";
        const string k_ShowStatusBarShortcutName = "Search/Dependency Graph Viewer/Show Status Bar";

        GUIStyle m_StatusBarStyle;
        GUIStyle m_NodeDescriptionStyle;
        GUIStyleState m_NodeDescriptionStyleState;

        internal void OnEnable()
        {
            titleContent = new GUIContent("Dependency Graph", EditorGUIUtility.FindTexture("Search Icon"));
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

        void Import(ICollection<UnityEngine.Object> objects)
        {
            Add(objects, ViewCenter());
            if (graphLayout == null)
                SetDefaultLayout();
        }

        void Add(ICollection<UnityEngine.Object> objects, in Vector2 pos)
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
                graph.AddNodes(n, deps.ToArray(), LinkType.DirectOut, null);

                var refs = db.GetResourceReferences(depID).Where(did => graph.FindNode(did) != null);
                graph.AddNodes(n, refs.ToArray(), LinkType.DirectIn, null);
            }

            if (graphLayout == null)
                SetDefaultLayout();
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

        Rect GetNodeInitialPosition(Graph graphModel, Vector2 offset)
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

        void HandleEvent(Event e)
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
                graph.RemoveNode(selectedNode);
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

        void HandleDragPerform(in Event e)
        {
            Add(DragAndDrop.objectReferences, LocalToView(e.mousePosition));
        }

        bool IsDragTargetValid()
        {
            return DragAndDrop.objectReferences.Any(o => AssetDatabase.GetAssetPath(o) != null);
        }

        Color GetLinkColor(in LinkType linkType)
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

        void DrawEdge(in Rect viewportRect, in Edge edge, in Vector2 from, in Vector2 to, in EdgeDisplay edgeDisplay)
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

            #if UNITY_2022_2_OR_NEWER
            switch (edgeDisplay)
            {
                case EdgeDisplay.Bezier:
                    Handles.DrawBezier(from, to,
                        new Vector2(from.x + kHalfNodeWidth, from.y),
                        new Vector2(to.x - kHalfNodeWidth, to.y),
                        edgeColor, null, 5f);
                    break;
                case EdgeDisplay.Elbow:
                    DrawElbowEdge(edge, from, to, edgeColor, 5f);
                    break;
            }
            #else
            Handles.DrawBezier(from, to,
                        new Vector2(from.x + kHalfNodeWidth, from.y),
                        new Vector2(to.x - kHalfNodeWidth, to.y),
                        edgeColor, null, 5f);
            #endif
        }

        #if UNITY_2022_2_OR_NEWER
        void DrawElbowEdge(in Edge edge, in Vector2 from, in Vector2 to, in Color edgeColor, in float edgeWidth)
        {
            var sourceRect = edge.Source.rect.OffsetBy(pan);
            var targetRect = edge.Target.rect.OffsetBy(pan);
            var points = new List<Vector3>();

            if (sourceRect.xMax <= targetRect.xMin)
            {
                var diff = to - from;
                points.Add(from);

                if (diff.y != 0)
                {
                    // Add a segment to middle point
                    var halfPointFrom = from + new Vector2(diff.x / 2, 0);
                    var halfPointTo = to - new Vector2(diff.x / 2, 0);

                    AddElbowEdgeCornerPoints(from, halfPointFrom, halfPointTo, points, kElbowCornerRadius, false, true);
                    AddElbowEdgeCornerPoints(halfPointFrom, halfPointTo, to, points, kElbowCornerRadius, true, false);
                }

                // In segment
                points.Add(to);
            }
            else
            {
                var anchorOffset = new Vector2(kNodeMargin * 2, 0f);
                var fromOutsidePoint = from + anchorOffset;
                var toOutsidePoint = to - anchorOffset;

                points.Add(from);

                var cornerSource = new Vector2(fromOutsidePoint.x, (sourceRect.center.y + targetRect.center.y) / 2f);

                if (sourceRect.VerticalOverlaps(targetRect))
                {
                    if (targetRect.yMin > sourceRect.yMin)
                    {
                        // Elbow over
                        cornerSource.y = sourceRect.yMin - kNodeMargin;
                    }
                    else
                    {
                        // Elbow under
                        cornerSource.y = sourceRect.yMax + kNodeMargin;
                    }
                }
                var cornerTarget = new Vector2(toOutsidePoint.x, cornerSource.y);

                AddElbowEdgeCornerPoints(from, fromOutsidePoint, cornerSource, points, kElbowCornerRadius, false, true);
                AddElbowEdgeCornerPoints(fromOutsidePoint, cornerSource, cornerTarget, points, kElbowCornerRadius, true, true);
                AddElbowEdgeCornerPoints(cornerSource, cornerTarget, toOutsidePoint, points, kElbowCornerRadius, true, true);
                AddElbowEdgeCornerPoints(cornerTarget, toOutsidePoint, to, points, kElbowCornerRadius, true, false);

                // In segment
                points.Add(to);
            }

            Handles.DrawAAPolyLine(edgeWidth, Enumerable.Repeat(edgeColor, points.Count).ToArray(), points.ToArray());
        }

        void AddElbowEdgeCornerPoints(in Vector2 from, in Vector2 corner, in Vector2 to, List<Vector3> points, float cornerRadius, bool fromCorner, bool toCorner)
        {
            if (cornerRadius == 0)
            {
                points.Add(corner);
                return;
            }

            var fromDiff = from - corner;
            var toDiff = to - corner;
            if (fromDiff.magnitude <= cornerRadius * (fromCorner ? 2f : 1f) || toDiff.magnitude <= cornerRadius * (toCorner ? 2f : 1f))
            {
                cornerRadius = Mathf.Min(fromDiff.magnitude / (fromCorner ? 2.1f : 1.1f), toDiff.magnitude / (toCorner ? 2.1f : 1.1f));
            }

            if (cornerRadius < 1f)
            {
                points.Add(corner);
                return;
            }

            var startOffset = (from - corner).normalized * cornerRadius;
            var endOffset = (to - corner).normalized * cornerRadius;
            var startPoint = corner + startOffset;
            var endPoint = corner + endOffset;
            var pivotPoint = corner + startOffset + endOffset;

            var startRelDir = (startPoint - pivotPoint).normalized;
            var endRelDir = (endPoint - pivotPoint).normalized;
            var startAngle = NormalizeAngle(Mathf.Atan2(-startRelDir.y, startRelDir.x));
            var endAngle = NormalizeAngle(Mathf.Atan2(-endRelDir.y, endRelDir.x));

            var deltaAngle = Mathf.Deg2Rad * 90f;

            points.Add(startPoint);
            var nbPoint = 10;
            if (cornerRadius < 2f)
                nbPoint = 2;
            var angleIncrement = deltaAngle / (nbPoint + 1);
            if (startAngle > endAngle)
                angleIncrement *= -1f;
            // Flip it for this special case where one angle is 0 and the other is 3*Pi/2
            if (startRelDir == Vector2.right && endRelDir == Vector2.up || startRelDir == Vector2.up && endRelDir == Vector2.right)
                angleIncrement *= -1f;
            for (var i = 1; i <= nbPoint; ++i)
            {
                var angle = startAngle + i * angleIncrement;
                var newPoint = new Vector2(Mathf.Cos(angle), -Mathf.Sin(angle)) * cornerRadius + pivotPoint;
                points.Add(newPoint);
            }

            points.Add(endPoint);
        }

        float NormalizeAngle(float angle)
        {
            if (angle < 0f)
                return angle + Mathf.PI * 2;
            return angle;
        }
        #endif

        protected void DrawNode(Event evt, in Rect viewportRect, Node node)
        {
            var windowRect = new Rect(node.rect.position + pan, node.rect.size);
            if (!node.rect.Overlaps(viewportRect))
                return;

            node.rect = GUI.Window(node.id, windowRect, _ => DrawNodeWindow(windowRect, evt, node), string.Empty);
            if (node.rect.Contains(evt.mousePosition))
            {
                if (string.IsNullOrEmpty(status))
                    Repaint();
                status = node.tooltip;
            }

            node.rect.x -= pan.x;
            node.rect.y -= pan.y;
        }

        void DrawNodeWindow(in Rect windowRect, Event evt, in Node node)
        {
            var nodeRect = new Rect(0, 0, windowRect.width, windowRect.height).PadBy(kBorderWidth);

            // Header
            var headerRect = new Rect(nodeRect.x, nodeRect.y, nodeRect.width, kNodeHeaderHeight);
            var borderRadius2 = new Vector4(kBorderRadius, kBorderRadius, 0, 0);
            GUI.DrawTexture(headerRect, EditorGUIUtility.whiteTexture, ScaleMode.StretchToFill, false, 0f, Colors.nodeHeader, Vector4.zero, borderRadius2);

            // Preview
            var hasPreview = node.preview != null;
            var previewSize = kNodeHeaderHeight - 2 * kNodeMargin;
            if (evt.type == EventType.Repaint && hasPreview)
            {
                GUI.DrawTexture(new Rect(
                    headerRect.x + kNodeMargin, headerRect.y + kNodeMargin,
                    previewSize, previewSize), node.preview);
            }

            // Title
            var titleHeight = 20f;
            var titleOffsetX = kNodeMargin;
            var titleWidth = headerRect.width - 2 * kNodeMargin;
            if (hasPreview)
            {
                titleOffsetX += previewSize + kNodeMargin;
                titleWidth = titleWidth - previewSize - kNodeMargin;
            }
            var nodeTitleRect = new Rect(titleOffsetX, kNodeMargin, titleWidth, titleHeight);
            GUI.Label(nodeTitleRect, node.title);

            // Description
            var descriptionHeight = 16f;
            var descriptionOffsetX = kNodeMargin;
            var descriptionWidth = headerRect.width - 2 * kNodeMargin;
            if (hasPreview)
            {
                descriptionOffsetX += previewSize + kNodeMargin;
                descriptionWidth = titleWidth - previewSize - kNodeMargin;
            }
            var nodeDescriptionRect = new Rect(descriptionOffsetX, nodeTitleRect.yMax, descriptionWidth, descriptionHeight);
            GUI.Label(nodeDescriptionRect, "In Project", m_NodeDescriptionStyle);

            // Expand Dependencies
            var buttonStyle = GUI.skin.button;
            var expandDependenciesContent = new GUIContent($"{node.dependencyCount}");
            var buttonContentSize = buttonStyle.CalcSize(expandDependenciesContent);
            var buttonRect = new Rect(nodeRect.width - kNodeMargin - buttonContentSize.x, nodeRect.height - kNodeMargin - kExpandButtonHeight, buttonContentSize.x, kExpandButtonHeight);
            if (GUI.Button(buttonRect, expandDependenciesContent))
            {
                if (!node.expandedDependencies)
                    graph.ExpandNodeDependencies(node);
                else
                    graph.RemoveNodeDependencies(node);
                graphLayout.Calculate(new GraphLayoutParameters { graph = graph, deltaTime = 0.05f, expandedNode = node });
                ComputeNewMinZoomLevel();
            }

            // Expand References
            var expandReferencesContent = new GUIContent($"{node.referenceCount}");
            buttonContentSize = buttonStyle.CalcSize(expandReferencesContent);
            buttonRect = new Rect(nodeRect.x + kNodeMargin, nodeRect.height - kNodeMargin - kExpandButtonHeight, buttonContentSize.x, kExpandButtonHeight);
            if (GUI.Button(buttonRect, expandReferencesContent))
            {
                if (!node.expandedReferences)
                    graph.ExpandNodeReferences(node);
                else
                    graph.RemoveNodeReferences(node);
                graphLayout.Calculate(new GraphLayoutParameters { graph = graph, deltaTime = 0.05f, expandedNode = node });
                ComputeNewMinZoomLevel();
            }

            // Pin
            buttonRect = new Rect(nodeRect.width - kBorderWidth * 2 - 16, kBorderWidth * 2, 16, 16);
            node.pinned = EditorGUI.Toggle(buttonRect, node.pinned);

            // Handle events
            if (evt.type == EventType.MouseDown && nodeRect.Contains(evt.mousePosition))
            {
                if (evt.button == 0)
                {
                    var selectedObject = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(node.name);
                    if (evt.clickCount == 1)
                    {
                        selectedNode = node;
                        if (selectedObject)
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

        void DrawGraph(Event evt)
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
                    DrawEdge(viewportRect, edge, GetNodeDependencyAnchorPoint(edge.Source) + pan, GetNodeReferenceAnchorPoint(edge.Target) + pan, currentEdgeDisplay);
                Handles.EndGUI();
            }

            BeginWindows();
            foreach (var node in graph.nodes)
                DrawNode(evt, viewportRect, node);
            EndWindows();
        }

        void DrawView(Event evt)
        {
            var worldBoundRect = this.rootVisualElement.worldBound;
            EditorZoomArea.Begin(zoom, graphRect, worldBoundRect);
            DrawGraph(evt);
            EditorZoomArea.End();
        }

        void DrawHUD(Event evt)
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

                foreach (var edgeDisplayName in Enum.GetNames(typeof(EdgeDisplay)))
                {
                    menu.AddItem(new GUIContent($"Edge/{edgeDisplayName}"), false, () => SetEdgeDisplay((EdgeDisplay)Enum.Parse(typeof(EdgeDisplay), edgeDisplayName)));
                }

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

        Vector2 GetNodeDependencyAnchorPoint(Node node)
        {
            var nodeRect = node.rect.PadBy(kBorderWidth);
            return new Vector2(nodeRect.xMax - kNodeMargin, nodeRect.yMax - kNodeMargin - kExpandButtonHeight / 2f);
        }

        Vector2 GetNodeReferenceAnchorPoint(Node node)
        {
            var nodeRect = node.rect.PadBy(kBorderWidth);
            return new Vector2(nodeRect.xMin + kNodeMargin, nodeRect.yMax - kNodeMargin - kExpandButtonHeight / 2f);
        }

        void ClearGraph()
        {
            graph.Clear();
            SetMinZoomLevel(kBaseZoomMinLevel);
        }

        void Relayout()
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

        void SetDefaultLayout()
        {
            SetLayout(new DependencyColumnLayout());
        }

        void SetEdgeDisplay(EdgeDisplay edgeDisplay)
        {
            currentEdgeDisplay = edgeDisplay;
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
            win.position = DependencyUtils.GetMainWindowCenteredPosition(new Vector2(800, 500));
            win.Show();
        }

        #if UNITY_2022_2_OR_NEWER
        [Shortcut("Help/Search/Dependency Nodes", typeof(DependencyGraphViewer), KeyCode.Space)]
        internal static void SearchGraphNode(ShortcutArguments args)
        {
            if (args.context is not DependencyGraphViewer depGraphViewer)
                return;

            var context = SearchService.CreateContext(CreateSearchGraphNodeProvider(depGraphViewer));
            context.options |= SearchFlags.OpenContextual;
            context.options &= ~SearchFlags.Dockable;
            context.options &= ~SearchFlags.ReuseExistingWindow;
            var viewState = new SearchViewState(context,
                #if UNITY_2023_1_OR_NEWER
                UnityEngine.Search.SearchViewFlags.Borderless |
                #endif
                UnityEngine.Search.SearchViewFlags.DisableSavedSearchQuery |
                UnityEngine.Search.SearchViewFlags.DisableInspectorPreview |
                UnityEngine.Search.SearchViewFlags.Centered);
            viewState.sessionName = nameof(DependencyGraphViewer);
            viewState.title = "dependency node";

            var rect = depGraphViewer.m_Parent.window.position;
            viewState.position = Utils.GetCenteredWindowPosition(rect, new Vector2(350, 500));
            SearchService.ShowWindow(viewState);
        }

        static SearchProvider CreateSearchGraphNodeProvider(DependencyGraphViewer depGraphViewer)
        {
            const string providerId = "__sgn";
            var qe = new QueryEngine<Node>();
            qe.SetSearchDataCallback(SearchDependencyNodeName);
            return new SearchProvider("__sgn", "Dependency Nodes", (context, provider) => SearchDependencyNodes(context, provider, qe, depGraphViewer))
            {
                actions = new List<SearchAction>()
                {
                    new SearchAction(providerId, "select", null, null, items => SelectSearchItemNodes(depGraphViewer, items))
                }
            };
        }

        static void SelectSearchItemNodes(DependencyGraphViewer depGraphViewer, in SearchItem[] items)
        {
            var nodes = items.Select(item => item.data as Node).Where(n => n != null);
            var region = DependencyGraphUtils.GetBoundingBox(items.Select(item => item.data as Node));
            if (items.Length == 1)
                region = new Rect(region.center - region.size, region.size * 2f);
            depGraphViewer.selectedNode ??= nodes.FirstOrDefault();
            depGraphViewer.FrameRegion(region);
        }

        static IEnumerable<string> SearchDependencyNodeName(Node n)
        {
            if (!string.IsNullOrEmpty(n.name))
                yield return n.name;
            if (!string.IsNullOrEmpty(n.typeName))
                yield return n.typeName;
            yield return n.id.ToString();
        }

        static IEnumerable<SearchItem> SearchDependencyNodes(SearchContext context, SearchProvider provider, QueryEngine<Node> qe, DependencyGraphViewer depGraphViewer)
        {
            var query = qe.Parse(context.searchQuery, useFastYieldingQueryHandler: true);
            if (!query.valid)
                return Enumerable.Empty<SearchItem>();

            var graph = depGraphViewer.graph;
            return query.Apply(graph.nodes).Where(n => n != null).Select(n => CreateNodeSearchItem(context, provider, n));
        }

        static SearchItem CreateNodeSearchItem(in SearchContext context, in SearchProvider provider, in Node n)
        {
            return provider.CreateItem(context, n.id.ToString(), n.index, n.title ?? n.name, n.tooltip, n.preview as Texture2D, n);
        }
        #endif
    }
}
