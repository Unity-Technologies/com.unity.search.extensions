using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace UnityEditor.Search
{
    class DependencyColumnLayout : IGraphLayout
    {
        public float MinColumnWidth { get; set; }
        public float ColumnPadding { get; set; }
        public float InnerColumnPadding { get; set; }

        public float MinRowHeight { get; set; }
        public float RowPadding { get; set; }

        public int MaxNodePerColumn { get; set; }

        public bool Animated => false;

        public DependencyColumnLayout()
        {
            MinColumnWidth = 300f;
            MinRowHeight = 150f;
            ColumnPadding = 100;
            InnerColumnPadding = 10;
            RowPadding = 25f;
            MaxNodePerColumn = 100;
        }

        public bool Calculate(GraphLayoutParameters parameters)
        {
            var graph = parameters.graph;
            var nodes = graph.nodes;
            if (nodes.Count == 0)
                return false;

            var nodesToProcess = new HashSet<int>(nodes.Select(n => n.id));
            var connectedComponents = new List<List<Node>>();

            foreach (var node in nodes)
            {
                if (!nodesToProcess.Contains(node.id))
                    continue;

                nodesToProcess.Remove(node.id);
                var cc = new List<Node>();
                cc.Add(node);
                var neighbors = graph.GetNeighbors(node.id, true);
                var nodeQueue = new Queue<Node>(neighbors);
                while (nodeQueue.Count > 0)
                {
                    var neighbor = nodeQueue.Dequeue();
                    if (!nodesToProcess.Contains(neighbor.id))
                        continue;
                    nodesToProcess.Remove(neighbor.id);
                    cc.Add(neighbor);
                    neighbors = graph.GetNeighbors(neighbor.id, true);
                    foreach (var n in neighbors)
                    {
                        if (!nodesToProcess.Contains(n.id))
                            continue;
                        nodeQueue.Enqueue(n);
                    }
                }

                connectedComponents.Add(cc);
            }

            var targetPosition = new Vector2(0, 0);
            if (parameters.expandedNode != null)
                targetPosition = parameters.expandedNode.rect.position;

            var allNodesBB = DependencyGraphUtils.GetBoundingBox(nodes);
            var offset = allNodesBB.position;
            foreach (var cc in connectedComponents)
            {
                LayoutConnectedComponent(graph, cc, offset);
                var bb = DependencyGraphUtils.GetBoundingBox(cc);
                offset.y += bb.height + RowPadding;
            }

            if (parameters.expandedNode != null)
            {
                nodesToProcess = new HashSet<int>(nodes.Select(n => n.id));
                var additionalOffset = targetPosition - parameters.expandedNode.rect.position;
                foreach (var node in nodes)
                {
                    if (!nodesToProcess.Contains(node.id))
                        continue;
                    nodesToProcess.Remove(node.id);
                    var originalPosition = node.rect.position;
                    node.SetPosition(originalPosition.x + additionalOffset.x, originalPosition.y + additionalOffset.y);
                }
            }

            return false;
        }

        void LayoutConnectedComponent(Graph graph, List<Node> nodes, in Vector2 offset)
        {
            if (nodes.Count == 0)
                return;
            if (nodes.Count == 1)
            {
                nodes[0].SetPosition(offset.x, offset.y);
                return;
            }

            var nodeByLevels = LayoutGraph.ProcessLevels(graph, nodes);

            var x = offset.x;
            var y = offset.y;
            foreach (var level in nodeByLevels.Keys.OrderBy(k => k))
            {
                var levelNodes = nodeByLevels[level];
                var columnInfo = GetColumnInfo(levelNodes);
                var nodeCount = 0;
                var innerColumnCount = 0;
                foreach (var levelNode in levelNodes)
                {
                    if (nodeCount == MaxNodePerColumn)
                    {
                        ++innerColumnCount;
                        x += columnInfo.width + InnerColumnPadding;
                        y = offset.y + innerColumnCount * RowPadding;
                        nodeCount = 0;
                    }
                    levelNode.SetPosition(x, y);
                    y += levelNode.rect.height + RowPadding;
                    ++nodeCount;
                }

                x += columnInfo.width + ColumnPadding;
                y = offset.y;
            }
        }

        struct ColumnInfo
        {
            public float width;
            public float height;
        }

        ColumnInfo GetColumnInfo(List<Node> columnNodes)
        {
            var maxWidth = 0f;
            var height = 0f;
            foreach (var node in columnNodes)
            {
                maxWidth = Mathf.Max(node.rect.width, maxWidth);
                height += node.rect.height + RowPadding;
            }

            return new ColumnInfo() { height = height, width = maxWidth };
        }

        interface ILayoutNode
        {
            int id { get; }
            int level { get; }
            bool needsUpdate { get; set; }

            IEnumerable<Node> GetNodes();

            void ChangeLevel(int newLevel);

            bool HasReferences();

            List<Node> GetDependencies();
            List<Node> GetReferences();
            void PopulateNodeByIds(Dictionary<int, ILayoutNode> nodeByIds);
        }

        class LayoutNode : ILayoutNode
        {
            public int id => node.id;
            public int level { get; private set; }

            public Node node { get; private set; }

            public bool needsUpdate { get; set; }

            Graph m_Graph;

            public LayoutNode(Node node, Graph graph)
            {
                this.node = node;
                this.level = 0;
                this.needsUpdate = true;
                m_Graph = graph;
            }

            public IEnumerable<Node> GetNodes()
            {
                return new[] { node };
            }

            public void ChangeLevel(int newLevel)
            {
                level = newLevel;
                needsUpdate = true;
            }

            public bool HasReferences()
            {
                return m_Graph.HasReferences(node.id, true);
            }

            public List<Node> GetDependencies()
            {
                return m_Graph.GetDependencies(node.id, true);
            }

            public List<Node> GetReferences()
            {
                return m_Graph.GetReferences(node.id, true);
            }

            public void PopulateNodeByIds(Dictionary<int, ILayoutNode> nodeByIds)
            {
                nodeByIds.Add(node.id, this);
            }
        }

        class LayoutNodeCycle : ILayoutNode
        {
            public int id => m_Cycle[0].id;
            public int level { get; private set; }
            public bool needsUpdate { get; set; }

            Graph m_Graph;
            List<Node> m_Cycle;
            HashSet<int> m_Ids;

            public LayoutNodeCycle(List<Node> cycle, Graph graph)
            {
                m_Cycle = cycle;
                level = 0;
                needsUpdate = true;
                m_Graph = graph;
                m_Ids = new HashSet<int>(cycle.Select(n => n.id));
            }

            public IEnumerable<Node> GetNodes()
            {
                return m_Cycle;
            }

            public void ChangeLevel(int newLevel)
            {
                level = newLevel;
                needsUpdate = true;
            }

            public bool HasReferences()
            {
                return GetReferences().Count > 0;
            }

            public List<Node> GetDependencies()
            {
                return RemoveLocalNodes(m_Cycle.SelectMany(node => m_Graph.GetDependencies(node.id, true))).ToList();
            }

            public List<Node> GetReferences()
            {
                return RemoveLocalNodes(m_Cycle.SelectMany(node => m_Graph.GetReferences(node.id, true))).ToList();
            }

            IEnumerable<Node> RemoveLocalNodes(IEnumerable<Node> nodes)
            {
                return nodes.Where(n => !m_Ids.Contains(n.id));
            }

            public void PopulateNodeByIds(Dictionary<int, ILayoutNode> nodeByIds)
            {
                foreach (var node in m_Cycle)
                {
                    nodeByIds.Add(node.id, this);
                }
            }
        }

        class LayoutGraph
        {

            public LayoutGraph()
            {}

            public static Dictionary<int, List<Node>> ProcessLevels(Graph graph, List<Node> nodes)
            {
                var layoutNodes = GetLayoutNodes(graph, nodes);
                var nodeByIds = new Dictionary<int, ILayoutNode>();
                foreach (var layoutNode in layoutNodes)
                {
                    layoutNode.PopulateNodeByIds(nodeByIds);
                }

                // Start with the roots only
                var nodesToProcess = new Queue<int>(layoutNodes.Where(n => !n.HasReferences()).Select(n => n.id));

                // Iterate over all nodes and update their possible level.
                // We keep iterating until no more nodes change.
                // When we update the level of a node, we push all other neighbors
                // on the stack to re-update them.
                while (nodesToProcess.Count > 0)
                {
                    var currentNodeId = nodesToProcess.Dequeue();
                    var currentNode = nodeByIds[currentNodeId];
                    var currentLevel = currentNode.level;
                    if (!currentNode.needsUpdate)
                        continue;
                    currentNode.needsUpdate = false;

                    var deps = currentNode.GetDependencies();
                    var refs = currentNode.GetReferences();

                    foreach (var dep in deps)
                    {
                        var depNode = nodeByIds[dep.id];
                        if (depNode.level < currentLevel + 1)
                        {
                            depNode.ChangeLevel(currentLevel + 1);
                            nodesToProcess.Enqueue(dep.id);
                        }
                    }
                }

                // Compress levels if possible
                foreach (var node in nodeByIds.Values)
                {
                    var deps = node.GetDependencies();
                    var refs = node.GetReferences();

                    var maxLevel = int.MaxValue;
                    var minLevel = int.MinValue;
                    foreach (var dep in deps)
                    {
                        var depNode = nodeByIds[dep.id];
                        maxLevel = Math.Min(maxLevel, depNode.level - 1);
                    }
                    foreach (var @ref in refs)
                    {
                        var refNode = nodeByIds[@ref.id];
                        minLevel = Math.Max(minLevel, refNode.level + 1);
                    }

                    if (minLevel == int.MinValue && maxLevel == int.MaxValue)
                        continue;
                    if (minLevel != int.MinValue && maxLevel != int.MaxValue)
                        continue;
                    if (minLevel == int.MinValue)
                        node.ChangeLevel(maxLevel);
                    else
                        node.ChangeLevel(minLevel);
                }

                var nodesByLevel = new Dictionary<int, List<Node>>();
                foreach (var kvp in nodeByIds)
                {
                    var node = kvp.Value;
                    var level = node.level;
                    if (!nodesByLevel.ContainsKey(level))
                        nodesByLevel.Add(level, new List<Node>());
                    nodesByLevel[level].AddRange(node.GetNodes());
                }

                return nodesByLevel;
            }

            static List<ILayoutNode> GetLayoutNodes(Graph graph, List<Node> nodes)
            {
                // Find the strongly connected components to regroup cycles into
                // logical nodes.
                var index = 0;
                var nodeStack = new Stack<Node>();
                var nodeIndexes = new Dictionary<int, int>();
                var nodeLowLinks = new Dictionary<int, int>();
                var nodesOnStack = new HashSet<int>();
                var layoutNodes = new List<ILayoutNode>();

                foreach (var node in nodes)
                {
                    if (!nodeIndexes.ContainsKey(node.id))
                        StrongConnect(node, graph, ref index, nodeStack, nodeIndexes, nodeLowLinks, nodesOnStack, layoutNodes);
                }

                return layoutNodes;
            }

            static void StrongConnect(Node node, Graph graph, ref int index, Stack<Node> nodeStack, Dictionary<int, int> nodeIndexes, Dictionary<int, int> nodeLowLinks, HashSet<int> nodesOnStack, List<ILayoutNode> layoutNodes)
            {
                nodeIndexes.TryAdd(node.id, index);
                nodeLowLinks.TryAdd(node.id, index);
                ++index;
                nodeStack.Push(node);
                nodesOnStack.Add(node.id);

                var deps = graph.GetDependencies(node.id, true);
                foreach (var dep in deps)
                {
                    if (!nodeIndexes.ContainsKey(dep.id))
                    {
                        StrongConnect(dep, graph, ref index, nodeStack, nodeIndexes, nodeLowLinks, nodesOnStack, layoutNodes);
                        nodeLowLinks[node.id] = Math.Min(nodeLowLinks[node.id], nodeLowLinks[dep.id]);
                    }
                    else if (nodesOnStack.Contains(dep.id))
                    {
                        // The line below is not a mistake. It is actually correct that we compare against dep index instead of lowLink.
                        nodeLowLinks[node.id] = Math.Min(nodeLowLinks[node.id], nodeIndexes[dep.id]);
                    }
                }

                if (nodeLowLinks[node.id] == nodeIndexes[node.id])
                {
                    var stronglyConnectedComponent = new List<Node>();
                    Node poppedNode = null;
                    do
                    {
                        poppedNode = nodeStack.Pop();
                        nodesOnStack.Remove(poppedNode.id);
                        stronglyConnectedComponent.Add(poppedNode);
                    } while (poppedNode.id != node.id);

                    if (stronglyConnectedComponent.Count == 0)
                        return;

                    if (stronglyConnectedComponent.Count == 1)
                    {
                        var layoutNode = new LayoutNode(stronglyConnectedComponent[0], graph);
                        layoutNodes.Add(layoutNode);
                    }
                    else
                    {
                        var cycleNode = new LayoutNodeCycle(stronglyConnectedComponent, graph);
                        layoutNodes.Add(cycleNode);
                    }
                }
            }
        }
    }
}
