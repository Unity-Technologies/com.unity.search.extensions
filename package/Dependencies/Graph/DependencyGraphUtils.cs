using System.Collections.Generic;
using UnityEngine;

namespace UnityEditor.Search
{
    static class DependencyGraphUtils
    {
        public static Rect GetBoundingBox(IEnumerable<Node> nodes)
        {
            var xMin = float.MaxValue;
            var yMin = float.MaxValue;
            var xMax = float.MinValue;
            var yMax = float.MinValue;
            foreach (var node in nodes)
            {
                if (node == null)
                    continue;

                if (node.rect.xMin < xMin)
                    xMin = node.rect.xMin;
                if (node.rect.xMax > xMax)
                    xMax = node.rect.xMax;
                if (node.rect.yMin < yMin)
                    yMin = node.rect.yMin;
                if (node.rect.yMax > yMax)
                    yMax = node.rect.yMax;
            }

            var bb = Rect.MinMaxRect(xMin, yMin, xMax, yMax);
            return bb;
        }
    }
}
