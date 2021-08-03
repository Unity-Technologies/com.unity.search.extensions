using System.Collections.Generic;
using UnityEngine;

namespace UnityEditor.Search
{
    class DependencyColumnLayout : IGraphLayout
    {
        private readonly Node m_AssetNode;
        private readonly List<Node> m_DependencyNodes;
        private readonly List<Node> m_ReferenceNodes;

        public float MinColumnWidth { get; set; }
        public float ColumnPadding { get; set; }

        public float MinRowHeight { get; set; }
        public float RowPadding { get; set; }

        public bool Animated { get { return false; } }

        public DependencyColumnLayout(Node assetNode, List<Node> dependencies, List<Node> references)
        {
            m_AssetNode = assetNode;
            m_DependencyNodes = dependencies;
            m_ReferenceNodes = references;

            MinColumnWidth = 300f;
            MinRowHeight = 150f;
            ColumnPadding = 100;
            RowPadding = 25f;
        }

        public bool Calculate(Graph graph, float deltaTime)
        {
            // Assume m_AssetNode position is fixed and that we position both column around it.

            var maxReferenceWidth = 0f;
            var referencesHeight = 0f;
            foreach (var refNode in m_ReferenceNodes)
            {
                maxReferenceWidth = Mathf.Max(refNode.rect.width, maxReferenceWidth);
                referencesHeight += refNode.rect.height + RowPadding;
            }

            var maxDepWidth = 0f;
            var dependenciesHeight = 0f;
            foreach (var depNode in m_DependencyNodes)
            {
                maxDepWidth = Mathf.Max(depNode.rect.width, maxDepWidth);
                dependenciesHeight += depNode.rect.height + RowPadding;
            }


            // Column: center height on AssetNode
            var x = m_AssetNode.rect.x - maxReferenceWidth - ColumnPadding;
            var y = m_AssetNode.rect.y - referencesHeight / 2.0f;
            foreach (var refNode in m_ReferenceNodes)
            {
                refNode.SetPosition(x, y);
                y += refNode.rect.height + RowPadding;
            }

            // Column: center height on AssetNode
            x += maxReferenceWidth + ColumnPadding + m_AssetNode.rect.width + ColumnPadding;
            y = m_AssetNode.rect.y - dependenciesHeight / 2.0f;
            foreach (var depNode in m_DependencyNodes)
            {
                depNode.SetPosition(x, y);
                y += depNode.rect.height + RowPadding;
            }

			return false;
        }
    }
}
