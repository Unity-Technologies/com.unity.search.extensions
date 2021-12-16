using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace UnityEditor.Search
{
    static class ThreadUtils
    {
        public static int GetBatchSizeByCore(int totalSize, int minSizePerCore = 8)
        {
            return Math.Max(totalSize / Environment.ProcessorCount, minSizePerCore);
        }

        public static ParallelLoopResult ParallelFor(int startInclusive, int endExclusive, Action<int, int> callback)
        {
            var batchSize = GetBatchSizeByCore(endExclusive - startInclusive);
            return Parallel.ForEach(Partitioner.Create(startInclusive, endExclusive), range => callback(range.Item1, range.Item2));
        }

        public static ParallelLoopResult ParallelForAggregate<TResult>(int startInclusive, int endExclusive,
            Func<TResult> initLocal, Func<int, int, ParallelLoopState, TResult, TResult> callback, Action<TResult> localFinally)
        {
            var batchSize = GetBatchSizeByCore(endExclusive - startInclusive);
            return Parallel.ForEach(Partitioner.Create(startInclusive, endExclusive),
                initLocal,
                (range, parallelLoopState, initialValue) => callback(range.Item1, range.Item2, parallelLoopState, initialValue),
                localFinally);
        }
    }
}
