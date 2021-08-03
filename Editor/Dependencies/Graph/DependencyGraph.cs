using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace UnityEditor.Search
{
    enum LinkType : uint
    {
        Self,
        WeakIn,
        WeakOut,
        DirectIn,
        DirectOut
    }

    class Node
    {
        public DependencyDatabase db;

        // Common data
        public int id;
        public int index;
        public string name;
        public string typeName;

        // UI data
        public Rect rect;
        public bool previewFetched = false;
        public Texture cachedPreview = null;
        public Texture preview
        {
            get
            {
                if (!previewFetched)
                {
                    cachedPreview = db.GetResourcePreview(id);
                    previewFetched = true;
                }
                if (cachedPreview)
                    return cachedPreview;
                cachedPreview = null;
                return AssetDatabase.GetCachedIcon(name);
            }
        }
        public LinkType linkType;
        public string title;
        public string tooltip;

        // Layouting data
        public bool pinned;
        public bool expanded;
        public float mass = 1.0f;

        public void SetPosition(float x, float y)
        {
            rect.x = x;
            rect.y = y;
        }
    }

    class Edge
    {
        public Edge(string id, Node source, Node target, LinkType linkType, float length = 1.0f)
        {
            ID = id;
            Source = source;
            Target = target;
            Directed = false;
            this.linkType = linkType;
            this.length = length;
        }

        public readonly string ID;
        public bool hidden = false;
        public LinkType linkType;
        public float length;
        public Node Source;
        public Node Target;
        public bool Directed;
    }

    class Graph
    {
        public DependencyDatabase db;
        public List<Node> nodes = new List<Node>();
        public List<Edge> edges = new List<Edge>();

        public Func<Graph, Vector2, Rect> nodeInitialPositionCallback = (graph, offset) => new Rect(0, 0, 100, 100);

        public Graph(DependencyDatabase db)
        {
            this.db = db;
        }

        public void Clear()
        {
            nodes.Clear();
            edges.Clear();
        }

        public Node FindNode(int nodeId)
        {
            // TODO: Could be more performant
            return nodes.Find(n => n.id == nodeId);
        }

        public List<Node> GetDependencies(int nodeId)
        {
            return edges.Where(e => e.Source.id == nodeId).Select(e => e.Target).ToList();
        }

        public List<Node> GetReferences(int nodeId)
        {
            return edges.Where(e => e.Target.id == nodeId).Select(e => e.Source).ToList();
        }

        public List<Node> GetNeighbors(int nodeId)
        {
            List<Node> neighbors = new List<Node>();
            foreach (var edge in edges)
            {
                if (edge.Target.id == nodeId)
                {
                    neighbors.Add(edge.Source);
                }
                else if (edge.Source.id == nodeId)
                {
                    neighbors.Add(edge.Target);
                }
            }
            return neighbors;
        }

        public Edge GetEdgeBetweenNodes(Node srcNode, Node dstNode)
        {
            foreach (var edge in edges)
            {
                if (edge.Source.id == srcNode.id && edge.Target.id == dstNode.id)
                    return edge;
            }
            return null;
        }

		public Node Add(int resourceId, Vector2 offset)
		{
			// Create root node
			var node = CreateNode(resourceId, nodes.Count, LinkType.Self, offset);

			// Add root node
			nodes.Add(node);
			return node;
		}

		public Node BuildGraph(int resourceId, Vector2 offset)
        {
            // Create root node
            var rootNode = CreateNode(resourceId, nodes.Count, LinkType.Self, offset);

            // Add root node
            nodes.Add(rootNode);

            // Expand it
            ExpandNode(rootNode);

            return rootNode;
        }

        private Node CreateNode(int id, int index, LinkType linkType, Vector2 offset)
        {
            string resourceName = db.GetResourceName(id);
            string typeName = db.GetResourceTypeName(id);
            string displayName = Path.GetFileNameWithoutExtension(resourceName);
            string tooltip = String.Format("{0} ({1})", resourceName, typeName);

            return new Node
            {
                db = db,
                id = id,
                index = index,
                linkType = linkType,
                name = resourceName,
                typeName = Path.GetExtension(resourceName),
                pinned = linkType == LinkType.Self,
                mass = linkType == LinkType.Self ? 1000 : (linkType == LinkType.DirectOut ? 10.0f : 20.0f),
                rect = nodeInitialPositionCallback(this, offset),
                title = displayName,
                tooltip = tooltip
            };
        }

        private Node GetOrCreateNode(int id, int index, LinkType linkType, Vector2 offset)
        {
            // TODO: optimize me
            var found = nodes.Find(node => node.id == id);
            if (found != null)
                return found;
            return CreateNode(id, index, linkType, offset);
        }

        private bool IsLinkOut(LinkType linkType)
        {
            if (linkType == LinkType.DirectOut || linkType == LinkType.WeakOut)
                return true;
            return false;
        }

        public void AddNodes(Node root, int[] deps, LinkType linkType, ISet<Node> addedNodes)
        {
            Dictionary<string, List<Node>> nmap = new Dictionary<string, List<Node>>();
            foreach (var id in deps)
            {
                var addedNode = GetOrCreateNode(id, nodes.Count, linkType, root.rect.center);
				addedNodes?.Add(addedNode);
				nodes.Add(addedNode);

                if (!nmap.ContainsKey(addedNode.typeName))
                    nmap[addedNode.typeName] = new List<Node>();
                nmap[addedNode.typeName].Add(addedNode);

                if (GetEdgeBetweenNodes(root, addedNode) == null)
                {
                    edges.Add(new Edge(root.id.ToString() + id,
                            IsLinkOut(linkType) ? root : addedNode, !IsLinkOut(linkType) ? root : addedNode, linkType,
                            linkType == LinkType.DirectOut ? 300 : 400));
                }
            }

            foreach (var p in nmap)
            {
                if (p.Value.Count < 3)
                    continue;

                Node pn = p.Value[0];
                for (int i = 1; i < p.Value.Count; ++i)
                {
                    Node cn = p.Value[i];
                    if (GetEdgeBetweenNodes(cn, pn) == null)
                        edges.Add(new Edge(pn.id.ToString() + cn.id, pn, cn, LinkType.WeakOut, 100) { hidden = true });
                    pn = cn;
                }
            }
        }

        public ISet<Node> ExpandNode(Node node)
        {
			node.pinned = true;
            var resourceId = node.id;
			var addedNodes = new HashSet<Node>()/* { node }*/;
			//addedNodes.UnionWith(GetNeighbors(node.id));
			AddNodes(node, db.GetResourceDependencies(resourceId), LinkType.DirectOut, addedNodes);
            AddNodes(node, db.GetResourceReferences(resourceId), LinkType.DirectIn, addedNodes);
            AddNodes(node, db.GetWeakDependencies(resourceId), LinkType.WeakOut, addedNodes);

            node.expanded = true;
			return addedNodes;
		}
    }
}
