using System;

namespace UnityEditor.Search
{
    static class ThreadUtils
    {
        public static int GetBatchSizeByCore(int totalSize, int minSizePerCore = 8)
        {
            return Math.Max(totalSize / Environment.ProcessorCount, minSizePerCore);
        }
    }
}
