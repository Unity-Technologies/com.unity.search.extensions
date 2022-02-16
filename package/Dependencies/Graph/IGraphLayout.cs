using System.Collections.Generic;

namespace UnityEditor.Search
{
    struct GraphLayoutParameters
    {
        public Graph graph;
        public float deltaTime;
        public Node expandedNode;
    }

    interface IGraphLayout
    {
        bool Animated { get; }
        bool Calculate(GraphLayoutParameters parameters);
    }
}
