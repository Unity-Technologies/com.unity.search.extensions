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

        public bool Animated { get { return false; } }

        public DependencyColumnLayout()
        {
            MinColumnWidth = 300f;
            MinRowHeight = 150f;
            ColumnPadding = 100;
            RowPadding = 25f;
        }

        public bool Calculate(Graph graph, float deltaTime)
        {
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

            var offset = new Vector2(0, 0);
            foreach (var cc in connectedComponents)
            {
                LayoutConnectedComponent(graph, cc, offset);
                var bb = DependencyGraphUtils.GetBoundingBox(cc);
                offset.y += bb.height + RowPadding;
            }

            return false;
        }

        void LayoutConnectedComponent(Graph graph, List<Node> nodes, in Vector2 offset)
        {
            if (nodes.Count <= 1)
                return;

            var nodesToProcess = new HashSet<int>(nodes.Select(n => n.id));
            var nodeLevelSet = new NodesByLevelSet();
            nodeLevelSet.Initialize();
            nodeLevelSet.AddNode(0, nodes[0]);
            nodesToProcess.Remove(nodes[0].id);
            var nodeQueue = new Stack<Node>();
            foreach (var n in graph.GetNeighbors(nodes[0].id, true))
            {
                nodeQueue.Push(n);
            }
            while (nodeQueue.Count > 0)
            {
                var node = nodeQueue.Pop();
                if (!nodesToProcess.Contains(node.id))
                    continue;
                nodesToProcess.Remove(node.id);

                var deps = graph.GetDependencies(node.id, true);
                var refs = graph.GetReferences(node.id, true);

                var minLevel = int.MaxValue;
                var maxLevel = int.MinValue;
                foreach (var dep in deps)
                {
                    if (nodeLevelSet.TryGetNodeLevel(dep, out var depLevel))
                        minLevel = Math.Min(minLevel, depLevel);
                    if (nodesToProcess.Contains(dep.id))
                        nodeQueue.Push(dep);
                }

                foreach (var @ref in refs)
                {
                    if (nodeLevelSet.TryGetNodeLevel(@ref, out var refLevel))
                        maxLevel = Math.Max(maxLevel, refLevel);
                    if (nodesToProcess.Contains(@ref.id))
                        nodeQueue.Push(@ref);
                }

                var actualLevel = 0;
                if (minLevel != Int32.MaxValue && maxLevel != int.MinValue)
                {
                    actualLevel = Math.Min(minLevel - 1, maxLevel + 1);
                    if ((maxLevel + 1) >= (minLevel - 1))
                    {
                        actualLevel = maxLevel + 1;

                    }

                }
                else if (minLevel == Int32.MaxValue)
                    actualLevel = maxLevel + 1;
                else
                    actualLevel = minLevel - 1;

                nodeLevelSet.AddNode(actualLevel, node);
            }

            var x = offset.x;
            var y = offset.y;
            foreach (var level in nodeLevelSet.levelSet)
            {
                var levelNodes = level.nodes;
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

        class NodesByLevel
        {
            public int level;
            public List<Node> nodes;

            NodesByLevelSet m_ParentSet;

            public NodesByLevel(int level, NodesByLevelSet levelSet)
            {
                this.level = level;
                nodes = new List<Node>();
                m_ParentSet = levelSet;
            }

            public void AddNode(Node node)
            {
                nodes.Add(node);
            }
        }

        struct NodesByLevelComparer : IComparer<NodesByLevel>
        {
            public int Compare(NodesByLevel x, NodesByLevel y)
            {
                return x.level.CompareTo(y.level);
            }
        }

        class NodesByLevelSet
        {
            public List<NodesByLevel> levelSet;

            Dictionary<Node, NodesByLevel> m_NodesByLevel;

            static readonly NodesByLevelComparer k_Comparer = new NodesByLevelComparer();

            public NodesByLevelSet()
            {
                levelSet = new List<NodesByLevel>();
                m_NodesByLevel = new Dictionary<Node, NodesByLevel>();
            }

            public NodesByLevel Initialize()
            {
                var initialLevel = new NodesByLevel(0, this);
                levelSet.Add(initialLevel);
                return initialLevel;
            }

            public void AddNode(int level, Node node)
            {
                var index = FindLevelIndex(level);
                var nodes = levelSet[index];
                nodes.AddNode(node);
                m_NodesByLevel.Add(node, nodes);
            }

            public bool TryGetNodeLevel(Node node, out int level)
            {
                level = 0;
                if (!m_NodesByLevel.TryGetValue(node, out var nodes))
                    return false;

                level = nodes.level;
                return true;
            }

            int FindLevelIndex(int level)
            {
                var dummyLevel = new NodesByLevel(level, this);
                var index = levelSet.BinarySearch(dummyLevel, k_Comparer);
                if (index < 0)
                {
                    index = ~index;
                    levelSet.Insert(index, new NodesByLevel(level, this));
                }

                return index;
            }
        }
    }
}
#endif
