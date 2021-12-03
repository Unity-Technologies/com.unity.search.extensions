#if !USE_SEARCH_DEPENDENCY_VIEWER || USE_SEARCH_MODULE
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.Search;
using UnityEngine;

namespace DependencyGraphTests
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

    class BaseGraphTestCase
    {
        public TestDependencyDatabase db;
        public IEnumerable<DependencyItem> items { get; private set; }
        public Graph graph { get; }

        public BaseGraphTestCase(IEnumerable<DependencyItem> items, IEnumerable<DependencyLink> links)
        {
            db = new TestDependencyDatabase(items, links);
            this.items = items;
            this.graph = new Graph(db);
        }

        public BaseGraphTestCase(TestDependencyDatabase db)
        {
            this.db = db;
            this.items = db.items;
            this.graph = new Graph(db);
        }

        public override string ToString()
        {
            return db.ToString();
        }
    }
}
#endif
