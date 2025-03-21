#if !UNITY_7000_0_OR_NEWER
using System.Collections.Generic;

namespace UnityEditor.Search
{
    public struct GraphLayoutParameters
    {
        public Graph graph;
        public float deltaTime;
        public Node expandedNode;
    }

    public interface IGraphLayout
    {
        bool Animated { get; }
        bool Calculate(GraphLayoutParameters parameters);
    }
}
#endif