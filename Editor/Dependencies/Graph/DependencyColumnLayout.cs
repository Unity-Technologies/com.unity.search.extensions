#if USE_SEARCH_TABLE
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

        public float MinRowHeight { get; set; }
        public float RowPadding { get; set; }

        public bool Animated => false;

        public DependencyColumnLayout()
        {
            MinColumnWidth = 300f;
            MinRowHeight = 150f;
            ColumnPadding = 100;
            RowPadding = 25f;
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
                    Debug.LogFormat(LogType.Log, LogOption.NoStacktrace, null, $"Move \"{node.name}\" from ({originalPosition}) to ({node.rect.position})");
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

            var nodeByLevels = NodeLevelGraph.ProcessLevels(graph, nodes);

            var x = offset.x;
            var y = offset.y;
            foreach (var level in nodeByLevels.Keys.Order(k => k, true))
            {
                var levelNodes = nodeByLevels[level];
                var columnInfo = GetColumnInfo(levelNodes);
                foreach (var levelNode in levelNodes)
                {
                    levelNode.SetPosition(x, y);
                    y += levelNode.rect.height + RowPadding;
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

        class NodeLevel
        {
            public int level { get; private set; }
            public Node node { get; private set; }

            public bool needsUpdate { get; private set; }

            public NodeLevel(Node node)
            {
                this.node = node;
                this.level = 0;
                this.needsUpdate = true;
            }

            public void ChangeLevel(int newLevel)
            {
                level = newLevel;
                needsUpdate = true;
            }
        }

        class NodeLevelGraph
        {

            public NodeLevelGraph()
            {}

            public static Dictionary<int, List<Node>> ProcessLevels(Graph graph, List<Node> nodes)
            {
                var nodesById = nodes.ToDictionary(n => n.id, n => new NodeLevel(n));
                var nodesToProcess = new Stack<int>(nodes.Select(n => n.id));

                // Iterate over all nodes and update their possible level.
                // We keep iterating until no more nodes change.
                // When we update the level of a node, we push all other neighbors
                // on the stack to re-update them.
                while (nodesToProcess.Count > 0)
                {
                    var currentNodeId = nodesToProcess.Pop();
                    var currentNode = nodesById[currentNodeId];
                    var currentLevel = currentNode.level;
                    if (!currentNode.needsUpdate)
                        continue;

                    var deps = graph.GetDependencies(currentNodeId, true);
                    var refs = graph.GetReferences(currentNodeId, true);

                    foreach (var dep in deps)
                    {
                        var depNode = nodesById[dep.id];
                        if (depNode.level < currentLevel + 1)
                        {
                            depNode.ChangeLevel(currentLevel + 1);
                            nodesToProcess.Push(dep.id);
                        }
                    }

                    foreach (var @ref in refs)
                    {
                        var refNode = nodesById[@ref.id];
                        if (refNode.level > currentLevel - 1)
                        {
                            refNode.ChangeLevel(currentLevel + 1);
                            nodesToProcess.Push(@ref.id);
                        }
                    }
                }

                // Compress levels if possible
                foreach (var node in nodesById.Values)
                {
                    var deps = graph.GetDependencies(node.node.id, true);
                    var refs = graph.GetReferences(node.node.id, true);

                    var maxLevel = int.MaxValue;
                    var minLevel = int.MinValue;
                    foreach (var dep in deps)
                    {
                        var depNode = nodesById[dep.id];
                        maxLevel = Math.Min(maxLevel, depNode.level - 1);
                    }
                    foreach (var @ref in refs)
                    {
                        var refNode = nodesById[@ref.id];
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
                foreach (var kvp in nodesById)
                {
                    var node = kvp.Value;
                    var level = node.level;
                    if (!nodesByLevel.ContainsKey(level))
                        nodesByLevel.Add(level, new List<Node>());
                    nodesByLevel[level].Add(node.node);
                }

                return nodesByLevel;
            }
        }
    }
}
#endif
