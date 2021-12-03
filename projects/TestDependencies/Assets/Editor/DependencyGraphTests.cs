#if !USE_SEARCH_DEPENDENCY_VIEWER || USE_SEARCH_MODULE
using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor.Search;
using UnityEngine;

namespace DependencyGraphTests
{
    #region Operations
    interface IDependencyGraphOperationTest
    {
        void Execute(Graph graph);
        void Validate(Graph graph);

        string ToString();
    }

    class ClearGraphOperationTest : IDependencyGraphOperationTest
    {
        public void Execute(Graph graph)
        {
            graph.Clear();
        }

        public void Validate(Graph graph)
        {
            Assert.IsEmpty(graph.nodes, "Graph nodes should be empty after clear.");
            Assert.IsEmpty(graph.edges, "Graph edges should be empty after clear.");
        }

        public override string ToString()
        {
            return "Clear";
        }
    }

    class AddNodeOperationTest : IDependencyGraphOperationTest
    {
        int m_NodeId;
        Vector2 m_Offset;

        public AddNodeOperationTest(int nodeId)
        {
            m_NodeId = nodeId;
            m_Offset = Vector2.zero;
        }

        public void Execute(Graph graph)
        {
            graph.Add(m_NodeId, m_Offset);
        }

        public void Validate(Graph graph)
        {
            Assert.IsTrue(graph.nodes.Find(n => n.id == m_NodeId) != null, $"Node {m_NodeId} was not added properly.");
        }

        public override string ToString()
        {
            return $"Add Node {m_NodeId}";
        }
    }

    enum NodeLinkType
    {
        All,
        Dependencies,
        References
    }

    class ExpandOperationTest : IDependencyGraphOperationTest
    {
        protected int m_NodeId;
        protected IEnumerable<int> m_ExpectedNodeIds;
        protected HashSet<int> m_ActualNodeIds;
        protected NodeLinkType m_LinkType;

        public ExpandOperationTest(int node, IEnumerable<int> expectedNodes, NodeLinkType linkType)
        {
            m_NodeId = node;
            m_ExpectedNodeIds = expectedNodes;
            m_ActualNodeIds = new HashSet<int>();
            m_LinkType = linkType;
        }

        public ExpandOperationTest(int node, NodeLinkType linkType)
            : this(node, Array.Empty<int>(), linkType)
        {}

        public ExpandOperationTest(int node, int expectedNode, NodeLinkType linkType)
            : this(node, new[] { expectedNode }, linkType)
        {}

        public virtual void Execute(Graph graph)
        {
            var node = graph.FindNode(m_NodeId);
            Assert.IsNotNull(node, $"Could not find node {m_NodeId} to expand {GetTypeString()}.");
            ISet<Node> addedNodes = null;
            switch (m_LinkType)
            {
                case NodeLinkType.All:
                    addedNodes = graph.ExpandNode(node);
                    break;
                case NodeLinkType.Dependencies:
                    addedNodes = graph.ExpandNodeDependencies(node);
                    break;
                case NodeLinkType.References:
                    addedNodes = graph.ExpandNodeReferences(node);
                    break;
            }
            m_ActualNodeIds.UnionWith(addedNodes?.Select(n => n.id) ?? Array.Empty<int>());
        }

        public virtual void Validate(Graph graph)
        {
            CollectionAssert.AreEquivalent(m_ExpectedNodeIds, m_ActualNodeIds, $"Added nodes after expanding {GetTypeString()} don't match expectations.");

            var expandedNode = graph.FindNode(m_NodeId);
            Assert.IsNotNull(expandedNode);
            Assert.IsNotNull(expandedNode, $"Could not find expanded node {m_NodeId} after expanding {GetTypeString()}.");
            foreach (var addedNodeId in m_ExpectedNodeIds)
            {
                var addedNode = graph.FindNode(addedNodeId);
                Assert.IsNotNull(addedNode, $"Could not find added node {addedNodeId} after expanding {GetTypeString()}.");

                Edge edge = null;
                switch (m_LinkType)
                {
                    case NodeLinkType.Dependencies:
                        edge = graph.GetEdgeBetweenNodes(expandedNode, addedNode);
                        break;
                    case NodeLinkType.References:
                        edge = graph.GetEdgeBetweenNodes(addedNode, expandedNode);
                        break;
                    case NodeLinkType.All:
                        edge = graph.GetEdgeBetweenNodes(expandedNode, addedNode) ?? graph.GetEdgeBetweenNodes(addedNode, expandedNode);
                        break;
                }
                Assert.IsNotNull(edge, $"An edge has not been added between expanded node {m_NodeId} and added node {addedNodeId} when expanding {GetTypeString()}");
            }
        }

        public override string ToString()
        {
            return $"Expand {GetTypeString()} for node {m_NodeId}";
        }

        string GetTypeString()
        {
            switch (m_LinkType)
            {
                case NodeLinkType.All:
                    return "dependencies and references";
                case NodeLinkType.Dependencies:
                    return "dependencies";
                case NodeLinkType.References:
                    return "references";
            }

            return string.Empty;
        }
    }

    class CollapseOperationTest : ExpandOperationTest
    {
        int m_EdgeCountBeforeRemoval;
        List<Edge> m_EdgesToRemove;

        public CollapseOperationTest(int node, IEnumerable<int> expectedRemovedNodes, NodeLinkType linkType)
            : base(node, expectedRemovedNodes, linkType)
        {
            if (linkType == NodeLinkType.All)
                throw new ArgumentException("Expand type cannot be All", nameof(linkType));
        }

        public CollapseOperationTest(int node, NodeLinkType linkType)
            : this(node, Array.Empty<int>(), linkType)
        { }

        public CollapseOperationTest(int node, int expectedNode, NodeLinkType linkType)
            : this(node, new[] { expectedNode }, linkType)
        { }

        public override void Execute(Graph graph)
        {
            m_EdgeCountBeforeRemoval = graph.edges.Count(e => e.linkType.IsDirectLink());
            m_EdgesToRemove = graph.edges.Where(e => m_LinkType == NodeLinkType.Dependencies ? e.Source.id == m_NodeId : e.Target.id == m_NodeId).ToList();

            var node = graph.FindNode(m_NodeId);
            Assert.IsNotNull(node, $"Could not find node {m_NodeId} to Collapse {GetTypeString()}.");
            ISet<int> removedNodes = null;
            switch (m_LinkType)
            {
                case NodeLinkType.Dependencies:
                    removedNodes = graph.RemoveNodeDependencies(node);
                    break;
                case NodeLinkType.References:
                    removedNodes = graph.RemoveNodeReferences(node);
                    break;
            }
            m_ActualNodeIds.UnionWith(removedNodes?.ToArray() ?? Array.Empty<int>());
        }

        public override void Validate(Graph graph)
        {
            CollectionAssert.AreEquivalent(m_ExpectedNodeIds, m_ActualNodeIds, $"Added nodes after collapsing {GetTypeString()} don't match expectations.");

            var node = graph.FindNode(m_NodeId);
            Assert.IsNotNull(node);
            Assert.IsNotNull(node, $"Could not find collapsed node {m_NodeId} after collapsing {GetTypeString()}.");
            foreach (var removedNodeId in m_ExpectedNodeIds)
            {
                var removedNode = graph.FindNode(removedNodeId);
                Assert.IsNull(removedNode, $"Expected removed node {removedNodeId} was not removed from the graph.");
            }

            var newEdgeCount = graph.edges.Count(e => e.linkType.IsDirectLink());
            var expectedRemovedCount = m_EdgesToRemove.Count;
            var expectedEdgeCount = m_EdgeCountBeforeRemoval - expectedRemovedCount;
            Assert.AreEqual(expectedEdgeCount, newEdgeCount, $"The number of edges is invalid. It should be {expectedEdgeCount}.");

            foreach (var edge in m_EdgesToRemove)
            {
                var removedEdge = graph.edges.Find(e => e.ID == edge.ID);
                Assert.IsNull(removedEdge, $"The edge {edge.ID} was not removed after collapsing {GetTypeString()}.");
            }
        }

        public override string ToString()
        {
            return $"Collapse {GetTypeString()} for node {m_NodeId}";
        }

        string GetTypeString()
        {
            switch (m_LinkType)
            {
                case NodeLinkType.All:
                    return "dependencies and references";
                case NodeLinkType.Dependencies:
                    return "dependencies";
                case NodeLinkType.References:
                    return "references";
            }

            return string.Empty;
        }
    }
    #endregion

    class DependencyGraphTestCase : BaseGraphTestCase
    {
        public IEnumerable<IDependencyGraphOperationTest> operations;

        public DependencyGraphTestCase(TestDependencyDatabase db, params IDependencyGraphOperationTest[] operations)
            : base(db)
        {
            this.operations = operations;
        }

        public override string ToString()
        {
            return string.Join(" - ", operations);
        }
    }

    class DependencyGraphTests
    {
        static TestDependencyDatabase s_Db;

        static TestDependencyDatabase db
        {
            get
            {
                if (s_Db == null)
                    InitDb();
                return s_Db;
            }
        }

        static void InitDb()
        {
            s_Db = new TestDependencyDatabase(new[]
            {
                new DependencyItem(0, "SearchContextAttributeTest.cs"),
                new DependencyItem(1, "temp4.mat"),
                new DependencyItem(2, "Cube.prefab"),
                new DependencyItem(3, "unity_builtin_extra"),
                new DependencyItem(4, "unity_default_resources"),
                new DependencyItem(5, "TestSearchContextAttributeScene.unity"),
                new DependencyItem(6, "cure_materialistic norwalk.mat"),
                new DependencyItem(7, "Sphere.prefab"),
            }, new[]
            {
                new DependencyLink(2, 0),
                new DependencyLink(5, 2),
                new DependencyLink(2, 3),
                new DependencyLink(2, 4),
                new DependencyLink(5, 3),
                new DependencyLink(5, 4),
                new DependencyLink(5, 6),
                new DependencyLink(1, 3),
                new DependencyLink(7, 1)
            });
        }

        static IEnumerable<DependencyGraphTestCase> ClearTestCases()
        {
            yield return new DependencyGraphTestCase(db, new AddNodeOperationTest(0), new ClearGraphOperationTest());
            yield return new DependencyGraphTestCase(db, new AddNodeOperationTest(0), new AddNodeOperationTest(1), new ClearGraphOperationTest());
            yield return new DependencyGraphTestCase(db, new AddNodeOperationTest(2), new ExpandOperationTest(2, new[] { 0, 3, 4 }, NodeLinkType.Dependencies), new ClearGraphOperationTest());
        }
        [Test]
        public void Clear([ValueSource(nameof(ClearTestCases))] DependencyGraphTestCase test) => RunTest(test);

        static IEnumerable<DependencyGraphTestCase> ExpandDependenciesTestCases()
        {
            yield return new DependencyGraphTestCase(db, new AddNodeOperationTest(0), new ExpandOperationTest(0, NodeLinkType.Dependencies));
            yield return new DependencyGraphTestCase(db, new AddNodeOperationTest(2), new ExpandOperationTest(2, new[] { 0, 3, 4 }, NodeLinkType.Dependencies));
            yield return new DependencyGraphTestCase(db, new AddNodeOperationTest(2), new ExpandOperationTest(2, new[] { 0, 3, 4 }, NodeLinkType.Dependencies), new ExpandOperationTest(2, NodeLinkType.Dependencies));
            yield return new DependencyGraphTestCase(db, new AddNodeOperationTest(7), new ExpandOperationTest(7, new[] { 1 }, NodeLinkType.Dependencies), new ExpandOperationTest(1, 3, NodeLinkType.Dependencies));
            yield return new DependencyGraphTestCase(db, new AddNodeOperationTest(1), new AddNodeOperationTest(2), new ExpandOperationTest(1, 3, NodeLinkType.Dependencies), new ExpandOperationTest(2, new[] { 0, 4 }, NodeLinkType.Dependencies));
        }
        [Test]
        public void ExpandDependencies([ValueSource(nameof(ExpandDependenciesTestCases))] DependencyGraphTestCase test) => RunTest(test);

        static IEnumerable<DependencyGraphTestCase> ExpandReferencesTestCases()
        {
            yield return new DependencyGraphTestCase(db, new AddNodeOperationTest(7), new ExpandOperationTest(7, NodeLinkType.References));
            yield return new DependencyGraphTestCase(db, new AddNodeOperationTest(0), new ExpandOperationTest(0, 2, NodeLinkType.References));
            yield return new DependencyGraphTestCase(db, new AddNodeOperationTest(0), new ExpandOperationTest(0, 2, NodeLinkType.References), new ExpandOperationTest(0, NodeLinkType.References));
            yield return new DependencyGraphTestCase(db, new AddNodeOperationTest(0), new ExpandOperationTest(0, 2, NodeLinkType.References), new ExpandOperationTest(2, 5, NodeLinkType.References));
            yield return new DependencyGraphTestCase(db, new AddNodeOperationTest(0), new AddNodeOperationTest(4), new ExpandOperationTest(0, 2, NodeLinkType.References), new ExpandOperationTest(4, 5, NodeLinkType.References));
        }
        [Test]
        public void ExpandReferences([ValueSource(nameof(ExpandReferencesTestCases))] DependencyGraphTestCase test) => RunTest(test);

        static IEnumerable<DependencyGraphTestCase> ExpandNodesTestCases()
        {
            yield return new DependencyGraphTestCase(db, new AddNodeOperationTest(2), new ExpandOperationTest(2, new[] { 0, 3, 4, 5 }, NodeLinkType.All));
            yield return new DependencyGraphTestCase(db, new AddNodeOperationTest(0), new ExpandOperationTest(0, 2, NodeLinkType.References), new ExpandOperationTest(2, new[] { 3, 4 }, NodeLinkType.Dependencies));
        }
        [Test]
        public void ExpandNodes([ValueSource(nameof(ExpandNodesTestCases))] DependencyGraphTestCase test) => RunTest(test);

        static IEnumerable<DependencyGraphTestCase> RemoveDependenciesTestCases()
        {
            yield return new DependencyGraphTestCase(db, new AddNodeOperationTest(7), new ExpandOperationTest(7, 1, NodeLinkType.Dependencies), new CollapseOperationTest(7, 1, NodeLinkType.Dependencies));
            yield return new DependencyGraphTestCase(db, new AddNodeOperationTest(7), new ExpandOperationTest(7, 1, NodeLinkType.Dependencies), new ExpandOperationTest(1, 3, NodeLinkType.Dependencies), new CollapseOperationTest(7, NodeLinkType.Dependencies));
            yield return new DependencyGraphTestCase(db, new AddNodeOperationTest(2), new ExpandOperationTest(2, new[] { 0, 3, 4, 5 }, NodeLinkType.All), new CollapseOperationTest(2, new []{0, 3, 4}, NodeLinkType.Dependencies));
            yield return new DependencyGraphTestCase(db, new AddNodeOperationTest(2), new AddNodeOperationTest(1), new ExpandOperationTest(2, new[] { 0, 3, 4 }, NodeLinkType.Dependencies), new ExpandOperationTest(1, NodeLinkType.Dependencies), new CollapseOperationTest(2, new[] { 0, 4 }, NodeLinkType.Dependencies));
        }
        [Test]
        public void RemoveDependencies([ValueSource(nameof(RemoveDependenciesTestCases))] DependencyGraphTestCase test) => RunTest(test);

        static IEnumerable<DependencyGraphTestCase> RemoveReferencesTestCases()
        {
            yield return new DependencyGraphTestCase(db, new AddNodeOperationTest(7), new ExpandOperationTest(7, 1, NodeLinkType.Dependencies), new CollapseOperationTest(1, 7, NodeLinkType.References));
            yield return new DependencyGraphTestCase(db, new AddNodeOperationTest(7), new ExpandOperationTest(7, 1, NodeLinkType.Dependencies), new ExpandOperationTest(1, 3, NodeLinkType.Dependencies), new CollapseOperationTest(3, NodeLinkType.References));
            yield return new DependencyGraphTestCase(db, new AddNodeOperationTest(2), new ExpandOperationTest(2, new[] { 0, 3, 4, 5 }, NodeLinkType.All), new CollapseOperationTest(2, 5, NodeLinkType.References));
            yield return new DependencyGraphTestCase(db, new AddNodeOperationTest(0), new AddNodeOperationTest(3), new ExpandOperationTest(0, 2, NodeLinkType.References), new ExpandOperationTest(3, new[] { 5, 1 }, NodeLinkType.References), new CollapseOperationTest(3, new[] { 5, 1 }, NodeLinkType.References));
        }
        [Test]
        public void RemoveReferences([ValueSource(nameof(RemoveReferencesTestCases))] DependencyGraphTestCase test) => RunTest(test);

        static IEnumerable<DependencyGraphTestCase> CollapseNodesTestCases()
        {
            yield return new DependencyGraphTestCase(db, new AddNodeOperationTest(2), new ExpandOperationTest(2, new[] { 0, 3, 4 }, NodeLinkType.Dependencies), new CollapseOperationTest(0, NodeLinkType.References), new CollapseOperationTest(2, new[] { 3, 4 }, NodeLinkType.Dependencies));
        }
        [Test]
        public void CollapseNodes([ValueSource(nameof(CollapseNodesTestCases))] DependencyGraphTestCase test) => RunTest(test);

        static void RunTest(DependencyGraphTestCase test)
        {
            foreach (var operation in test.operations)
            {
                operation.Execute(test.graph);
                operation.Validate(test.graph);
            }
        }
    }
}
#endif
