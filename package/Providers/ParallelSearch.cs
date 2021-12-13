using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

using Object = UnityEngine.Object;

namespace UnityEditor.Search
{
    class ParallelDocument
    {
        public readonly string id;
        public readonly string meta;

        private string m_MetaContent;
        private string m_TextContent;

        public Match capture { get; internal set; }

        public ParallelDocument(string id, string meta)
        {
            this.id = id;
            this.meta = meta;
        }

        public IEnumerable<string> EnumerateContent()
        {
            if (m_MetaContent == null)
                LoadMetaContent();
            yield return string.Intern(m_MetaContent);

            if (m_TextContent == null)
                LoadTextContent();
            yield return string.Intern(m_TextContent);
        }

        private void LoadTextContent()
        {
            if (!File.Exists(id))
            {
                m_TextContent = string.Empty;
                return;
            }

            using (var file = new StreamReader(id))
            {
                var header = new char[5];
                if (file.ReadBlock(header, 0, header.Length) != header.Length ||
                    (header[0] != '{') &&
                    (header[0] != '%' || header[1] != 'Y' || header[2] != 'A' || header[3] != 'M' || header[4] != 'L'))
                {
                    m_TextContent = string.Empty;
                    return;
                }

                m_TextContent = file.ReadToEnd();
            }
        }

        private void LoadMetaContent()
        {
            if (!File.Exists(meta))
            {
                m_MetaContent = string.Empty;
                return;
            }

            m_MetaContent = File.ReadAllText(meta);
        }

        public override string ToString()
        {
            return id;
        }
    }

    static class ParallelSearch
    {
        const string id = "parallel";
        static readonly Regex PropertyFilterRx = new Regex(@"#([\w\d\.]+)");
        static readonly Dictionary<string, double> EnumValues = new Dictionary<string, double>();

        static QueryEngine<ParallelDocument> queryEngine;

        static string[] s_Roots;
        static string[] roots
        {
            get
            {
                if (s_Roots == null)
                    s_Roots = Utils.GetAssetRootFolders();
                return s_Roots;
            }
        }

        [SearchActionsProvider]
        public static IEnumerable<SearchAction> ActionHandlers()
        {
            return new[]
            {
                new SearchAction(id, "select", new GUIContent("Select"), (SearchItem[] items) => Selection.objects = items.Select(e => e.ToObject()).ToArray())
            };
        }

        [SearchItemProvider]
        public static SearchProvider CreateProvider()
        {
            queryEngine = new QueryEngine<ParallelDocument>();
            queryEngine.SetSearchDataCallback(SearchDocumentWords);

            queryEngine.AddFilter<string>(PropertyFilterRx, OnPropertyFilter, new[] { ":", "!=", "=", ">", "<", ">=", "<=" });

            SearchValue.SetupEngine(queryEngine);

            foreach (var te in TypeCache.GetTypesDerivedFrom<Enum>())
            {
                if (te.IsGenericType)
                    continue;
                var n = te.GetEnumNames();
                var v = te.GetEnumValues();
                for (int i = 0; i < n.Length; ++i)
                    EnumValues[n[i].ToLowerInvariant()] = Convert.ToDouble(v.GetValue(i));
            }

            return new SearchProvider(id, "Parallel")
            {
                filterId = "//",
                isExplicitProvider = true,
                fetchItems = FetchItems,
                fetchDescription = FetchDescription,
                fetchThumbnail = FetchThumbnail,
                toObject = ToObject
            };
        }

        static Object ToObject(SearchItem item, Type type)
        {
            if (item.data is ParallelDocument doc)
                return AssetDatabase.LoadMainAssetAtPath(doc.id);
            return null;
        }

        static IEnumerable<string> SearchDocumentWords(ParallelDocument document)
        {
            yield return document.id;
        }

        static bool OnPropertyFilter(ParallelDocument document, string propertyName, QueryFilterOperator op, string _param)
        {
            return document.EnumerateContent().Any(content => 
            {
                var YAMLPropertyRx = new Regex(@$"({propertyName}):\s+([^\r\n]+)", RegexOptions.IgnoreCase);
                var paramIsNumber = Utils.TryGetNumber(_param, out double paramNumber);
                return YAMLPropertyRx.Matches(content).Any((Match match) => 
                {
                    if (HasMatch(match, op, _param, paramIsNumber, paramNumber))
                    {
                        document.capture = match;
                        return true;
                    }

                    return false;
                });
            });
        }

        static bool HasMatch(in Match match, in QueryFilterOperator op, in string _param, in bool paramIsNumber, double paramNumber)
        {
            var value = match.Groups[2].Value;
            var valueIsNumber = Utils.TryGetNumber(value, out double n);
            if (paramIsNumber && valueIsNumber)
            {
                return NumberEqual(op, n, paramNumber);
            }
            else if (valueIsNumber && EnumValues.TryGetValue(_param.ToLowerInvariant(), out paramNumber))
            {
                return NumberEqual(op, n, paramNumber) || (((long)n & (long)paramNumber) != 0);
            }
            else if (!string.IsNullOrEmpty(value))
            {
                switch (op.type)
                {
                    case FilterOperatorType.Contains: return value.IndexOf(_param, StringComparison.OrdinalIgnoreCase) != -1;
                    case FilterOperatorType.Equal: return string.Equals(value, _param, StringComparison.OrdinalIgnoreCase);
                    case FilterOperatorType.NotEqual: return !string.Equals(value, _param, StringComparison.OrdinalIgnoreCase);
                    case FilterOperatorType.Lesser: return string.CompareOrdinal(value, _param) < 0;
                    case FilterOperatorType.Greater: return string.CompareOrdinal(value, _param) > 0;
                    case FilterOperatorType.LesserOrEqual: return string.CompareOrdinal(value, _param) <= 0;
                    case FilterOperatorType.GreaterOrEqual: return string.CompareOrdinal(value, _param) >= 0;
                }
            }

            return false;
        }

        static bool NumberEqual(in QueryFilterOperator op, in double n, in double paramNumber)
        {
            switch (op.type)
            {
                case FilterOperatorType.Equal: 
                    return Mathf.Approximately((float)n, (float)paramNumber);
                case FilterOperatorType.Contains: return n == paramNumber;
                case FilterOperatorType.NotEqual: return n != paramNumber;
                case FilterOperatorType.Lesser: return n < paramNumber;
                case FilterOperatorType.Greater: return n > paramNumber;
                case FilterOperatorType.LesserOrEqual: return n <= paramNumber;
                case FilterOperatorType.GreaterOrEqual: return n >= paramNumber;
            }

            return false;
        }

        static CancellationTokenSource s_CancelQuery;
        static IEnumerable<SearchItem> FetchItems(SearchContext context, List<SearchItem> items, SearchProvider provider)
        {
            if (context.empty)
                yield break;

            s_CancelQuery?.Cancel();
            s_CancelQuery?.Dispose();
            s_CancelQuery = new CancellationTokenSource();

            var roots = ParallelSearch.roots;
            var results = new ConcurrentBag<ParallelDocument>();
            var query = queryEngine.Parse(context.searchQuery);
            var cancelToken = s_CancelQuery.Token;
            var po = new ParallelOptions
            {
                CancellationToken = cancelToken,
                MaxDegreeOfParallelism = Math.Max(Environment.ProcessorCount - 2, 1)
            };

            var task = Task.Run(() => Parallel.ForEach(EnumerateDocuments(roots), po, doc => SearchDocument(doc, query, results)), cancelToken);

            while (!task.IsCompleted || !results.IsEmpty)
            {
                if (results.IsEmpty)
                    yield return null;
                else
                {
                    while (results.TryTake(out var r))
                        yield return provider.CreateItem(context, r.id, 0, null, null, null, r);
                }
            }

            s_CancelQuery?.Dispose();
            s_CancelQuery = null;
        }

        static IEnumerable<ParallelDocument> EnumerateDocuments(string[] roots)
        {
            using (new DebugTimer("EnumerateDocuments"))
            {
                foreach (var root in roots)
                {
                    var paths = Directory.EnumerateFiles(root, "*.meta", SearchOption.AllDirectories);
                    foreach (var path in paths)
                    {
                        var metaPath = path.Replace("\\", "/");
                        var assetPath = metaPath[0..^5];
                        yield return new ParallelDocument(assetPath, metaPath);
                    }
                }
            }
        }

        static void SearchDocument(ParallelDocument document, Query<ParallelDocument> query, ConcurrentBag<ParallelDocument> results)
        {
            if (!query.Test(document))
                return;

            results.Add(document);
        }

        static string FetchDescription(SearchItem item, SearchContext context)
        {
            if (item.data is ParallelDocument doc)
                return $"{doc.capture.Value} at {doc.capture.Index}";
            return null;
        }

        static Texture2D FetchThumbnail(SearchItem item, SearchContext context)
        {
            if (item.data is ParallelDocument doc)
                return AssetDatabase.GetCachedIcon(doc.id) as Texture2D;
            return null;
        }
    }
}
