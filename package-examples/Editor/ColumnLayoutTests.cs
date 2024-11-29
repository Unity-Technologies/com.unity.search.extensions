using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor.Search;
using UnityEngine;

namespace DependencyGraphTests.Layout
{
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

    class ColumnLayoutTestCase : BaseGraphTestCase
    {
        public Dictionary<int, int> expectedLevelsById;
        public IEnumerable<IDependencyGraphOperationTest> operations;

        public ColumnLayoutTestCase(IEnumerable<DependencyItem> items, IEnumerable<DependencyLink> links, IEnumerable<ColumnLayoutNodeTest> expectedLevels)
            : base(items, links)
        {
            expectedLevelsById = expectedLevels.ToDictionary(el => el.id, el => el.level);
            operations = null;
        }

        public ColumnLayoutTestCase(IEnumerable<DependencyItem> items, IEnumerable<DependencyLink> links, IEnumerable<ColumnLayoutNodeTest> expectedLevels, params IDependencyGraphOperationTest[] operations)
            : base(items, links)
        {
            expectedLevelsById = expectedLevels.ToDictionary(el => el.id, el => el.level);
            this.operations = operations;
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
            yield return new ColumnLayoutTestCase(new[]
            {
                new DependencyItem(0, "Cube.prefab"),
                new DependencyItem(1, "TestSearchContextAttributeScene.unity"),
                new DependencyItem(2, "unity_default_resources"),
                new DependencyItem(3, "cure_materialistic norwalk.mat"),
                new DependencyItem(4, "vigorous-bag.mat"),
                new DependencyItem(5, "SearchContextAttributeTest.cs"),
                new DependencyItem(6, "unity_builtin_extra"),
            }, new[]
            {
                new DependencyLink(0, 5),
                new DependencyLink(0, 6),
                new DependencyLink(0, 2),
                new DependencyLink(1, 3),
                new DependencyLink(1, 2),
                new DependencyLink(1, 6),
                new DependencyLink(1, 0),
                new DependencyLink(1, 4),
                new DependencyLink(3, 6)
            }, new[]
            {
                new ColumnLayoutNodeTest(0, 1),
                new ColumnLayoutNodeTest(1, 0),
                new ColumnLayoutNodeTest(2, 2),
                new ColumnLayoutNodeTest(3, 1),
                new ColumnLayoutNodeTest(4, 1),
                new ColumnLayoutNodeTest(5, 2),
                new ColumnLayoutNodeTest(6, 2)
            },
                new AddNodeOperationTest(5),
                new ExpandOperationTest(5, NodeLinkType.References),
                new ExpandOperationTest(0, NodeLinkType.Dependencies),
                new ExpandOperationTest(0, NodeLinkType.References),
                new ExpandOperationTest(1, NodeLinkType.Dependencies),
                new ExpandOperationTest(3, NodeLinkType.Dependencies),
                new CollapseOperationTest(1, NodeLinkType.Dependencies),
                new ExpandOperationTest(1, NodeLinkType.Dependencies));
        }

        [Test]
        public void NodeLevels([ValueSource(nameof(ColumnLayoutTestCases))] ColumnLayoutTestCase test)
        {
            var graph = test.graph;

            var nodes = new List<Node>();
            if (test.operations == null)
            {
                foreach (var item in test.items)
                {
                    var node = graph.Add(item.id, Vector2.zero);
                    nodes.Add(node);
                }
                foreach (var node in nodes)
                {
                    graph.ExpandNode(node);
                }
            }
            else
            {
                foreach (var operation in test.operations)
                {
                    operation.Execute(graph);
                }

                nodes = graph.nodes;
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
