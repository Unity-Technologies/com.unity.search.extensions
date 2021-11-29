#if USE_SEARCH_TABLE
using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor.Search;
using UnityEngine;

namespace DependencyGraphView.Layout
{
    readonly struct DependencyLink
    {
        public readonly int sourceId;
        public readonly int destinationId;

        public DependencyLink(int sourceId, int destId)
        {
            this.sourceId = sourceId;
            this.destinationId = destId;
        }
    }

    class TestDependencyDatabase : IDependencyDatabase
    {
        Dictionary<int, DependencyItem> m_Items;
        Dictionary<int, List<int>> m_Dependencies;
        Dictionary<int, List<int>> m_References;

        public List<DependencyItem> items => m_Items.Values.ToList();

        public TestDependencyDatabase(IEnumerable<DependencyItem> items, IEnumerable<DependencyLink> links)
        {
            m_Items = items.ToDictionary(i => i.id, i => i);
            m_Dependencies = new Dictionary<int, List<int>>();
            m_References = new Dictionary<int, List<int>>();
            foreach (var dependencyLink in links)
            {
                if (!m_Dependencies.TryGetValue(dependencyLink.sourceId, out _))
                    m_Dependencies.Add(dependencyLink.sourceId, new List<int>());
                m_Dependencies[dependencyLink.sourceId].Add(dependencyLink.destinationId);
                if (!m_References.TryGetValue(dependencyLink.destinationId, out _))
                    m_References.Add(dependencyLink.destinationId, new List<int>());
                m_References[dependencyLink.destinationId].Add(dependencyLink.sourceId);
            }
        }

        public string GetResourceName(int id)
        {
            if (!m_Items.TryGetValue(id, out var di))
                return null;
            return di.path;
        }

        public DependencyType GetResourceType(int id)
        {
            return DependencyType.Asset;
        }

        public string GetResourceTypeName(int id)
        {
            return "string";
        }

        public int[] GetResourceDependencies(int id)
        {
            if (!m_Dependencies.TryGetValue(id, out var deps))
                return Array.Empty<int>();
            return deps.ToArray();
        }

        public int[] GetResourceReferences(int id)
        {
            if (!m_References.TryGetValue(id, out var refs))
                return Array.Empty<int>();
            return refs.ToArray();
        }

        public int[] GetWeakDependencies(int id)
        {
            return Array.Empty<int>();
        }

        public Texture GetResourcePreview(int id)
        {
            return null;
        }

        public int FindResourceByName(in string path)
        {
            foreach (var di in m_Items.Values)
            {
                if (di.path == path)
                    return di.id;
            }

            return -1;
        }

        public override string ToString()
        {
            return $"{string.Join(", ", items.Select(i => $"({i.id}, {i.path})"))}";
        }
    }

    readonly struct ColumnLayoutNodeTest
    {
        public readonly int id;
        public readonly int level;

        public ColumnLayoutNodeTest(int id, int level)
        {
            this.id = id;
            this.level = level;
        }
    }

    class ColumnLayoutTestCase
    {
        public TestDependencyDatabase db;
        public Dictionary<int, int> expectedLevelsById;
        public IEnumerable<DependencyItem> items { get; private set; }

        public ColumnLayoutTestCase(IEnumerable<DependencyItem> items, IEnumerable<DependencyLink> links, IEnumerable<ColumnLayoutNodeTest> expectedLevels)
        {
            db = new TestDependencyDatabase(items, links);
            expectedLevelsById = expectedLevels.ToDictionary(el => el.id, el => el.level);
            this.items = items;
        }

        public override string ToString()
        {
            return db.ToString();
        }
    }

    class ColumnLayoutTests
    {

        static IEnumerable<ColumnLayoutTestCase> ColumnLayoutTestCases()
        {
            yield return new ColumnLayoutTestCase(new[]
            {
                new DependencyItem(0, "bob"),
                new DependencyItem(1, "bobby")
            }, new[]
            {
                new DependencyLink(0, 1)
            }, new[]
            {
                new ColumnLayoutNodeTest(0, 0),
                new ColumnLayoutNodeTest(1, 1)
            });
            yield return new ColumnLayoutTestCase(new[]
            {
                new DependencyItem(0, "bob"),
                new DependencyItem(1, "bobby"),
                new DependencyItem(2, "dude")
            }, new[]
            {
                new DependencyLink(0, 1),
                new DependencyLink(0, 2)
            }, new[]
            {
                new ColumnLayoutNodeTest(0, 0),
                new ColumnLayoutNodeTest(1, 1),
                new ColumnLayoutNodeTest(2, 1)
            });
            yield return new ColumnLayoutTestCase(new[]
            {
                new DependencyItem(0, "bob"),
                new DependencyItem(1, "bobby"),
                new DependencyItem(2, "dude"),
                new DependencyItem(3, "dudette")
            }, new[]
            {
                new DependencyLink(0, 1),
                new DependencyLink(0, 2),
                new DependencyLink(1, 3)
            }, new[]
            {
                new ColumnLayoutNodeTest(0, 0),
                new ColumnLayoutNodeTest(1, 1),
                new ColumnLayoutNodeTest(2, 1),
                new ColumnLayoutNodeTest(3, 2)
            });
            yield return new ColumnLayoutTestCase(new[]
            {
                new DependencyItem(0, "bob"),
                new DependencyItem(1, "bobby"),
                new DependencyItem(2, "dude"),
                new DependencyItem(3, "dudette"),
                new DependencyItem(4, "4")
            }, new[]
            {
                new DependencyLink(0, 1),
                new DependencyLink(0, 2),
                new DependencyLink(1, 3),
                new DependencyLink(4, 3)
            }, new[]
            {
                new ColumnLayoutNodeTest(0, 0),
                new ColumnLayoutNodeTest(1, 1),
                new ColumnLayoutNodeTest(2, 1),
                new ColumnLayoutNodeTest(3, 2),
                new ColumnLayoutNodeTest(4, 1)
            });
            yield return new ColumnLayoutTestCase(new[]
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
            }, new[]
            {
                new ColumnLayoutNodeTest(0, 2),
                new ColumnLayoutNodeTest(1, 1),
                new ColumnLayoutNodeTest(2, 1),
                new ColumnLayoutNodeTest(3, 2),
                new ColumnLayoutNodeTest(4, 2),
                new ColumnLayoutNodeTest(5, 0),
                new ColumnLayoutNodeTest(6, 1),
                new ColumnLayoutNodeTest(7, 0)
            });
            yield return new ColumnLayoutTestCase(new[]
            {
                new DependencyItem(0, "SearchContextAttributeTest.cs"),
                new DependencyItem(1, "temp4.mat"),
                new DependencyItem(2, "Cube.prefab"),
            }, new[]
            {
                new DependencyLink(0, 1),
                new DependencyLink(1, 2),
                new DependencyLink(2, 0),
            }, new[]
            {
                new ColumnLayoutNodeTest(0, 0),
                new ColumnLayoutNodeTest(1, 0),
                new ColumnLayoutNodeTest(2, 0),
            });
            yield return new ColumnLayoutTestCase(new[]
            {
                new DependencyItem(0, "root"),
                new DependencyItem(1, "SearchContextAttributeTest.cs"),
                new DependencyItem(2, "temp4.mat"),
                new DependencyItem(3, "Cube.prefab"),
            }, new[]
            {
                new DependencyLink(0, 1),
                new DependencyLink(1, 2),
                new DependencyLink(2, 3),
                new DependencyLink(3, 1)
            }, new[]
            {
                new ColumnLayoutNodeTest(0, 0),
                new ColumnLayoutNodeTest(1, 1),
                new ColumnLayoutNodeTest(2, 1),
                new ColumnLayoutNodeTest(3, 1),
            });
            yield return new ColumnLayoutTestCase(new[]
            {
                new DependencyItem(0, "SearchContextAttributeTest.cs"),
                new DependencyItem(1, "temp4.mat"),
                new DependencyItem(2, "Cube.prefab"),
                new DependencyItem(3, "leaf"),
            }, new[]
            {
                new DependencyLink(0, 1),
                new DependencyLink(1, 2),
                new DependencyLink(2, 0),
                new DependencyLink(2, 3)
            }, new[]
            {
                new ColumnLayoutNodeTest(0, 0),
                new ColumnLayoutNodeTest(1, 0),
                new ColumnLayoutNodeTest(2, 0),
                new ColumnLayoutNodeTest(3, 1),
            });
        }

        [Test]
        public void NodeLevels([ValueSource(nameof(ColumnLayoutTestCases))] ColumnLayoutTestCase test)
        {
            var graph = new Graph(test.db);

            var nodes = new List<Node>();
            foreach (var item in test.items)
            {
                var node = graph.Add(item.id, Vector2.zero);
                nodes.Add(node);
            }

            foreach (var node in nodes)
            {
                graph.ExpandNode(node);
            }

            var layout = new DependencyColumnLayout();
            layout.Calculate(new GraphLayoutParameters() { graph = graph, deltaTime = 0.05f });

            var nodesByLevel = new Dictionary<int, List<int>>();
            foreach (var node in nodes)
            {
                var nodeXPos = (int)Math.Round(node.rect.xMin, MidpointRounding.AwayFromZero);
                if (!nodesByLevel.ContainsKey(nodeXPos))
                    nodesByLevel.Add(nodeXPos, new List<int>());
                nodesByLevel[nodeXPos].Add(node.id);
            }

            var currentNodeLevel = 0;
            foreach (var xPos in nodesByLevel.Keys.OrderBy(k => k))
            {
                var currentLevelNodeIds = nodesByLevel[xPos];
                foreach (var currentLevelNodeId in currentLevelNodeIds)
                {
                    var nodeName = test.db.GetResourceName(currentLevelNodeId);
                    Assert.AreEqual(test.expectedLevelsById[currentLevelNodeId], currentNodeLevel, $"Node \"{nodeName}\" has an unexpected level.");
                }

                ++currentNodeLevel;
            }
        }
    }
}
#endif
