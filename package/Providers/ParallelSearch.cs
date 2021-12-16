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

        public string text => m_TextContent ?? LoadTextContent();

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

        private string LoadTextContent()
        {
            return (m_TextContent = LoadContent(id, validTextOnly: true));
        }

        private void LoadMetaContent()
        {
            m_MetaContent = LoadContent(meta, validTextOnly: false);
        }

        public static string LoadContent(string id, bool validTextOnly = true)
        {
            if (!File.Exists(id))
                return string.Empty;

            if (validTextOnly)
            {
                using (var file = new StreamReader(id))
                {
                    var header = new char[5];
                    if (file.ReadBlock(header, 0, header.Length) != header.Length ||
                        (header[0] != '{') &&
                        (header[0] != '%' || header[1] != 'Y' || header[2] != 'A' || header[3] != 'M' || header[4] != 'L'))
                    {
                        return string.Empty;
                    }

                    return file.ReadToEnd();
                }
            }
            else
            {
                return File.ReadAllText(id);
            }
        }

        public override string ToString()
        {
            return id;
        }
    }

    static class ParallelSearch
    {
        const string id = "parallel";
        static readonly Regex PropertyFilterRx = new Regex(@"#([\w\d\.]+)", RegexOptions.Compiled);
        static readonly Regex ObjectInfoRegex = new Regex(@"---\s!u!(\d+)\s&(-?\d+)[\r\n]+(\w+):", RegexOptions.Compiled);
        static readonly Dictionary<string, double> EnumValues = new Dictionary<string, double>();

        static readonly ConcurrentDictionary<string, string> PathsToGUID = new ConcurrentDictionary<string, string>();
        static readonly ConcurrentDictionary<string, Regex> RefRegexCache = new ConcurrentDictionary<string, Regex>();
        static readonly ConcurrentDictionary<string, Regex> PropertyRegexCache = new ConcurrentDictionary<string, Regex>();
        
        static QueryEngine<ParallelDocument> queryEngine;
        static CancellationTokenSource s_CancelQuery;
        
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
            queryEngine.SetSearchDataCallback(OnSearchDocumentWords);

            queryEngine.AddFilter<string>("ref", OnRefFilter, new string[] { ":", "=" });
            queryEngine.AddFilter<string>("from", OnFromFilter, new string[] { ":", "=" });
            queryEngine.AddFilter<string>("t", OnTypeFilter, new string[] { ":", "=", "!=" });
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
                fetchPreview = FetchPreview,
                showDetailsOptions = ShowDetailsOptions.Inspector | ShowDetailsOptions.Actions | ShowDetailsOptions.Description | ShowDetailsOptions.Preview,
                toObject = ToObject
            };
        }

        static IEnumerable<string> OnSearchDocumentWords(ParallelDocument document)
        {
            yield return document.id;
        }

        static bool OnTypeFilter(ParallelDocument document, QueryFilterOperator op, string paramtype)
        {
            string docType = string.Empty;
            var extPos = document.id.LastIndexOf('.');
            if (extPos != -1 && document.id.LastIndexOf('/') < extPos)
                docType = document.id[(extPos + 1)..];

            if (!string.IsNullOrEmpty(docType) && StringEqual(docType, op, paramtype))
                return true;

            if (TryGetMetaGUIDAndType(document.meta, out string _, out string typeName))
            {
                if (StringEqual(typeName, op, paramtype))
                    return true;
            }

            return EnumerateTypes(document.text, document).Any(typeName => StringEqual(typeName, op, paramtype));
        }

        static bool OnRefFilter(ParallelDocument document, QueryFilterOperator op, string paramValue)
        {
            var guid = ToGuid(paramValue);
            if (guid == null)
                return false;

            var refRx = GetRefGUIDRegex(guid);
            return document.EnumerateContent().Any(content =>
            {
                return refRx.Matches(content).Any((Match match) =>
                {
                    document.capture = match;
                    return true;
                });
            });
        }

        private static bool OnFromFilter(ParallelDocument document, QueryFilterOperator op, string paramValue)
        {
            var docGUID = ToGuid(document.id);
            if (docGUID == null)
                return false;

            return FindGUID(paramValue, docGUID) || FindGUID(paramValue + ".meta", docGUID);
        }

        static readonly ConcurrentDictionary<string, string> TextContentCache = new ConcurrentDictionary<string, string>();
        static bool FindGUID(in string path, in string searchGUID)
        {
            if (!TextContentCache.TryGetValue(path, out string content))
            {
                content = ParallelDocument.LoadContent(path, validTextOnly: true);
                if (!TextContentCache.TryAdd(path, content))
                    return false;
            }

            if (string.IsNullOrEmpty(content))
                return false;

            var refRx = GetRefGUIDRegex(searchGUID);
            return refRx.Matches(content).Count > 0;
        }

        static bool OnPropertyFilter(ParallelDocument document, string propertyName, QueryFilterOperator op, string paramValue)
        {
            var YAMLPropertyRx = GetPropertyRegex(propertyName);
            return document.EnumerateContent().Any(content => 
            {
                var paramIsNumber = Utils.TryGetNumber(paramValue, out double paramNumber);
                return YAMLPropertyRx.Matches(content).Any((Match match) => 
                {
                    if (HasMatch(match, op, paramValue, paramIsNumber, paramNumber))
                    {
                        document.capture = match;
                        return true;
                    }

                    return false;
                });
            });
        }

        static string ToGuid(string assetPath)
        {
            assetPath = assetPath.ToLowerInvariant();
            if (PathsToGUID.TryGetValue(assetPath, out var guid))
                return guid ?? assetPath;

            string metaFile = $"{assetPath}.meta";
            if (TryGetMetaGUIDAndType(metaFile, out guid, out _))
            {
                if (PathsToGUID.TryAdd(assetPath, guid))
                    return guid;
            }
            
            PathsToGUID.TryAdd(assetPath, null);
            return assetPath;
        }

        private static IEnumerable<string> EnumerateTypes(string textContent, ParallelDocument doc)
        {
            foreach (Match m in ObjectInfoRegex.Matches(textContent))
            {
                doc.capture = m;
                yield return m.Groups[3].Value;
            }
        }

        static bool TryGetMetaGUIDAndType(string id, out string guid, out string typeName)
        {
            guid = null;
            typeName = null;

            if (!File.Exists(id))
                return false;

            string line;
            using (var file = new StreamReader(id))
            {
                while ((line = file.ReadLine()) != null)
                {
                    if (!line.StartsWith("guid:", StringComparison.Ordinal))
                        continue;

                    guid = line[6..];

                    line = file.ReadLine();
                    if (line != null)
                        typeName = line[0..^9];
                    return true;
                }
            }

            return false;
        }

        static Regex GetRefGUIDRegex(in string guid)
        {
            if (RefRegexCache.TryGetValue(guid, out Regex rx))
                return rx;

            rx = new Regex(@$"guid:\s+({guid})", RegexOptions.Compiled | RegexOptions.CultureInvariant);
            RefRegexCache.TryAdd(guid, rx);
            return rx;
        }

        static Regex GetPropertyRegex(in string propertyName)
        {
            if (PropertyRegexCache.TryGetValue(propertyName, out Regex rx))
                return rx;

            var tokens = propertyName.Split('.');
            var pattern = @$"({propertyName}):\s+([^\r\n]+)";
            if (tokens.Length > 1)
            {
                // Metallic:([{\n\s]+|.+?)+?m_Scale:([{\n\s]+|.+?)+?y:\s+([^\r\n,}]+)                
                pattern = "";
                for (int i = 0; i < tokens.Length-1; i++)
                {
                    string t = tokens[i];
                    pattern += @$"{t}:([{{\r\n\s]+|.+?)+?";
                }
                pattern += @$"{tokens[^1]}:\s+([^\r\n,}}]+)";
            }

            rx = new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);
            PropertyRegexCache.TryAdd(propertyName, rx);
            return rx;
        }

        static bool HasMatch(in Match match, in QueryFilterOperator op, in string _param, in bool paramIsNumber, double paramNumber)
        {
            var value = match.Groups[^1].Value;
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

        static bool StringEqual(in string s1, in QueryFilterOperator op, in string s2)
        {
            switch (op.type)
            {
                case FilterOperatorType.Contains: return s1.IndexOf(s2, StringComparison.OrdinalIgnoreCase) != -1;
                case FilterOperatorType.Equal: return string.Equals(s1, s2, StringComparison.OrdinalIgnoreCase);
                case FilterOperatorType.NotEqual: return !string.Equals(s1, s2, StringComparison.OrdinalIgnoreCase);
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

        static IEnumerable<SearchItem> FetchItems(SearchContext context, List<SearchItem> items, SearchProvider provider)
        {
            if (context.empty)
                yield break;

            s_CancelQuery?.Cancel();
            s_CancelQuery?.Dispose();
            s_CancelQuery = new CancellationTokenSource();

            var query = queryEngine.Parse(context.searchQuery.Trim());
            var results = new ConcurrentBag<ParallelDocument>();
            var cancelToken = s_CancelQuery.Token;
            var po = new ParallelOptions
            {
                CancellationToken = cancelToken,
                MaxDegreeOfParallelism = Math.Max(Environment.ProcessorCount - 2, 1)
            };

            var roots = query.HasToggle("packages") ? ParallelSearch.roots : new string[] { "Assets" };
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

            while (results.TryTake(out var r))
                yield return provider.CreateItem(context, r.id, 0, null, null, null, r);

            s_CancelQuery?.Dispose();
            s_CancelQuery = null;
        }

        static IEnumerable<ParallelDocument> EnumerateDocuments(string[] roots)
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

        static void SearchDocument(ParallelDocument document, Query<ParallelDocument> query, ConcurrentBag<ParallelDocument> results)
        {
            if (!query.Test(document))
                return;

            results.Add(document);
        }

        static string FetchDescription(SearchItem item, SearchContext context)
        {
            if (item.data is ParallelDocument doc && doc.capture != null)
                return $"{doc.capture.Value.Replace("\n", " ").Trim()} at {doc.capture.Index}";
            return null;
        }

        static Texture2D FetchThumbnail(SearchItem item, SearchContext context)
        {
            if (item.data is ParallelDocument doc)
                return AssetDatabase.GetCachedIcon(doc.id) as Texture2D;
            return null;
        }

        static Texture2D FetchPreview(SearchItem item, SearchContext context, Vector2 size, FetchPreviewOptions options)
        {
            return AssetPreview.GetAssetPreview(item.ToObject<Texture>()) ?? AssetPreview.GetAssetPreviewFromGUID(item.id);
        }

        static Object ToObject(SearchItem item, Type type)
        {
            if (item.data is ParallelDocument doc)
                return AssetDatabase.LoadMainAssetAtPath(doc.id);
            return null;
        }
    }
}
