using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEditor;
using System.Threading;

namespace UnityEditor.Search
{
    // Syntax:
    // <guid>        => Yield asset with <guid>
    //
    // is:file       => Yield file assets
    // is:folder     => Yield folder assets
    // is:package    => Yield package assets
    // is:broken     => Yield assets that have at least one broken reference.
    // is:missing    => Yield GUIDs which are missing an valid asset (a GUID was found but no valid asset use that GUID)
    //
    // from:         => Yield assets which are used by asset with <guid>
    // in=<count>    => Yield assets which are used <count> times
    //
    // ref:<guid>    => Yield assets which are referencing the asset with <guid>
    // out=<count>   => Yield assets which have <count> references to other assets
    class DependencyIndexer : SearchIndexer
    {
        static readonly Regex[] guidRxs = new [] {
            new Regex(@"guid:\s+([a-z0-9]{32})"),
            new Regex(@"guid:\s+([a-z0-9]{8}-[a-z0-9]{4}-[a-z0-9]{4}-[a-z0-9]{4}-[a-z0-9]{12})")
        };
        static readonly Regex hash128Regex = new Regex(@"guid:\s+Value:\s+x:\s(\d+)\s+y:\s(\d+)\s+z:\s(\d+)\s+w:\s(\d+)");
        static readonly char[] k_HexToLiteral = { '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', 'a', 'b', 'c', 'd', 'e', 'f' };

        readonly ConcurrentDictionary<string, string> guidToPathMap = new ConcurrentDictionary<string, string>();
        readonly ConcurrentDictionary<string, string> pathToGuidMap = new ConcurrentDictionary<string, string>();
        readonly ConcurrentDictionary<string, string> aliasesToPathMap = new ConcurrentDictionary<string, string>();
        readonly ConcurrentDictionary<string, ConcurrentDictionary<string, byte>> guidToRefsMap = new ConcurrentDictionary<string, ConcurrentDictionary<string, byte>>();
        readonly ConcurrentDictionary<string, ConcurrentDictionary<string, byte>> guidFromRefsMap = new ConcurrentDictionary<string, ConcurrentDictionary<string, byte>>();
        readonly Dictionary<string, int> guidToDocMap = new Dictionary<string, int>();
        readonly HashSet<string> ignoredGuids = new HashSet<string>();

        readonly static string[] builtinGuids = new string[]
        {
            "0000000000000000d000000000000000",
            "0000000000000000e000000000000000",
            "0000000000000000f000000000000000"
        };

        public DependencyIndexer()
        {
            resolveDocumentHandler = ResolveAssetPath;
        }

        public void Build(int progressId, in string[] metaFiles)
        {
            int completed = 0;
            var totalCount = metaFiles.Length;
            Parallel.ForEach(metaFiles, (mf) => ProcessAsset(mf, progressId, ref completed, totalCount));

            completed = 0;
            var total = pathToGuidMap.Count + guidToRefsMap.Count + guidFromRefsMap.Count;
            foreach (var kvp in pathToGuidMap)
            {
                var guid = kvp.Value;
                var path = kvp.Key;

                Progress.Report(progressId, completed++, total, path);

                var di = AddGuid(guid, path);
                AddStaticProperty("is", Directory.Exists(path) ? "folder" : "file", di);
                if (path.StartsWith("Packages/", StringComparison.Ordinal))
                    AddStaticProperty("is", "package", di);
                AddWord(guid, guid.Length, 0, di);
            }

            foreach (var kvp in guidToRefsMap)
            {
                var guid = kvp.Key;
                var refs = kvp.Value.Keys;
                var di = AddGuid(guid);
                AddWord(guid, guid.Length, 0, di);

                Progress.Report(progressId, completed++, total, guid);

                AddNumber("out", refs.Count, 0, di);
                foreach (var r in refs)
                {
                    AddStaticProperty("ref", r, di, exact: true);
                    if (guidToPathMap.TryGetValue(r, out var toPath))
                        AddStaticProperty("ref", toPath, di, exact: true);
                }
            }

            foreach (var kvp in guidFromRefsMap)
            {
                var guid = kvp.Key;
                var refs = kvp.Value.Keys;
                var di = AddGuid(guid);

                Progress.Report(progressId, completed++, total, guid);

                AddNumber("in", refs.Count, 0, di);
                foreach (var r in refs)
                {
                    AddStaticProperty("from", r, di, exact: true);
                    if (guidToPathMap.TryGetValue(r, out var fromPath))
                        AddStaticProperty("from", fromPath, di, exact: true);
                }

                if (!guidToPathMap.TryGetValue(guid, out var path))
                {
                    AddStaticProperty("is", "missing", di);

                    foreach (var r in refs)
                    {
                        var refDocumentIndex = AddGuid(r);
                        AddStaticProperty("is", "broken", refDocumentIndex);
                        var refDoc = GetDocument(refDocumentIndex);
                        var refMetaData = GetMetaInfo(refDoc.id);
                        if (refMetaData == null)
                            SetMetaInfo(refDoc.id, $"Broken links {guid}");
                        else
                            SetMetaInfo(refDoc.id, $"{refMetaData}, {guid}");
                    }

                    var refString = string.Join(", ", refs.Select(r =>
                    {
                        if (guidToPathMap.TryGetValue(r, out var rp))
                            return rp;
                        return r;
                    }));
                    SetMetaInfo(guid, $"Refered by {refString}");
                }
            }

            Clear();
        }

        public void Setup()
        {
            Clear();

            var allGuids = AssetDatabase.FindAssets("a:all");
            ignoredGuids.UnionWith(AssetDatabase.FindAssets("l:Ignore"));
            foreach (var guid in allGuids.Concat(builtinGuids))
            {
                TrackGuid(guid);
                var assetPath = AssetDatabase.GUIDToAssetPath(guid);
                pathToGuidMap.TryAdd(assetPath, guid);
                guidToPathMap.TryAdd(guid, assetPath);
            }
        }

        public bool ResolveAssetPath(string guid, out string path)
        {
            if (guidToPathMap.TryGetValue(guid, out path))
                return true;

            path = AssetDatabase.GUIDToAssetPath(guid);
            if (!string.IsNullOrEmpty(path))
            {
                guidToPathMap[guid] = path;
                return true;
            }

            return false;
        }

        void Clear()
        {
            pathToGuidMap.Clear();
            aliasesToPathMap.Clear();
            guidToPathMap.Clear();
            guidToRefsMap.Clear();
            guidFromRefsMap.Clear();
            guidToDocMap.Clear();
            ignoredGuids.Clear();
        }

        void ProcessAsset(string metaFilePath, int progressId, ref int completed, int totalCount)
        {
            Interlocked.Increment(ref completed);
            var assetPath = metaFilePath.Replace("\\", "/").Substring(0, metaFilePath.Length - 5).ToLowerInvariant();
            if (!File.Exists(assetPath))
                return;

            var guid = ToGuid(assetPath);
            if (ignoredGuids.Contains(guid))
                return;

            Progress.Report(progressId, completed, totalCount, assetPath);

            TrackGuid(guid);
            pathToGuidMap.TryAdd(assetPath, guid);
            guidToPathMap.TryAdd(guid, assetPath);

            var dir = Path.GetDirectoryName(assetPath).ToLowerInvariant();
            var name = Path.GetFileNameWithoutExtension(assetPath).ToLowerInvariant();
            var ext = Path.GetExtension(assetPath).ToLowerInvariant();
            aliasesToPathMap.TryAdd(assetPath.ToLowerInvariant(), guid);
            aliasesToPathMap.TryAdd(name, guid);
            aliasesToPathMap.TryAdd(name + ext, guid);
            aliasesToPathMap.TryAdd(dir + "/" + name, guid);

            var mfc = File.ReadAllText(metaFilePath);
            ScanDependencies(guid, mfc);

            using (var file = new StreamReader(assetPath))
            {
                var header = new char[5];
                if (file.ReadBlock(header, 0, header.Length) == header.Length &&
                    header[0] == '%' && header[1] == 'Y' && header[2] == 'A' && header[3] == 'M' && header[4] == 'L')
                {
                    var ac = file.ReadToEnd();
                    ScanDependencies(guid, ac);
                }
            }

            if (ext == ".cs")
                ProcessScript(assetPath);
        }

        void ProcessScript(in string path)
        {
            var scriptGuid = ToGuid(path);
            if (string.IsNullOrEmpty(scriptGuid))
                return;
            int lineIndex = 1;
            var re = new Regex(@"""[\w\/\-\s\.]+""");
            foreach (var line in File.ReadLines(path))
            {
                var matches = re.Matches(line);
                foreach (Match m in matches)
                {
                    var parsedValue = m.Value.ToLowerInvariant().Trim('"');
                    if (aliasesToPathMap.TryGetValue(parsedValue, out var guid) && !string.Equals(guid, scriptGuid))
                    {
                        guidToRefsMap[scriptGuid].TryAdd(guid, 1);
                        guidFromRefsMap[guid].TryAdd(scriptGuid, 1);
                    }

                    if (guidToPathMap.TryGetValue(parsedValue.Replace("-", ""), out _))
                    {
                        guidToRefsMap[scriptGuid].TryAdd(parsedValue, 1);
                        guidFromRefsMap[parsedValue].TryAdd(scriptGuid, 1);
                    }
                }
                lineIndex++;
            }
        }

        void AddStaticProperty(string key, string value, int di, bool exact = false)
        {
            value = value.ToLowerInvariant();
            AddProperty(key, value, value.Length, value.Length, 0, di, false, exact);
        }

        void ScanDependencies(in string guid, in string content)
        {
            foreach (var guidRx in guidRxs)
            {
                foreach (Match match in guidRx.Matches(content))
                    ScanDependencies(match, guid);
            }

            foreach (Match match in hash128Regex.Matches(content))
            {
                if (Utils.TryParse(match.Groups[1].ToString(), out uint h1) && 
                    Utils.TryParse(match.Groups[2].ToString(), out uint h2) &&
                    Utils.TryParse(match.Groups[3].ToString(), out uint h3) &&
                    Utils.TryParse(match.Groups[4].ToString(), out uint h4))
                {
                    if (h1 == 0 && h2 == 0 && h3 == 0 && h4 == 0)
                        continue;
                    AddDependencies(Hash128ToString(h1, h2, h3, h4), guid);
                }
            }
        }

        static string Hash128ToString(params uint[] Value)
        {
            var chars = new char[32];
            for (int i = 0; i < 4; i++)
            {
                for (int j = 7; j >= 0; j--)
                {
                    uint cur = Value[i];
                    cur >>= (j * 4);
                    cur &= 0xF;
                    chars[i * 8 + j] = k_HexToLiteral[cur];
                }
            }

            return new string(chars, 0, 32);
        }

        bool ScanDependencies(in Match match, in string guid)
        {
            if (match.Groups.Count < 2)
                return false;
            var rg = match.Groups[1].Value.Replace("-", "");
            return AddDependencies(rg, guid);
        }

        bool AddDependencies(in string rg, in string guid)
        {
            if (rg == guid || ignoredGuids.Contains(rg))
                return false;

            TrackGuid(rg);
            guidToRefsMap[guid].TryAdd(rg, 0);
            guidFromRefsMap[rg].TryAdd(guid, 0);
            return true;
        }

        void TrackGuid(string guid)
        {
            if (!guidToRefsMap.ContainsKey(guid))
                guidToRefsMap.TryAdd(guid, new ConcurrentDictionary<string, byte>());

            if (!guidFromRefsMap.ContainsKey(guid))
                guidFromRefsMap.TryAdd(guid, new ConcurrentDictionary<string, byte>());
        }

        int AddGuid(in string guid, in string path = null)
        {
            if (guidToDocMap.TryGetValue(guid, out var di))
                return di;

            di = AddDocument(guid, null, path, checkIfExists: false, SearchDocumentFlags.Asset);
            guidToDocMap.Add(guid, di);
            return di;
        }

        string ToGuid(string assetPath)
        {
            if (pathToGuidMap.TryGetValue(assetPath, out var guid))
                return guid;

            string metaFile = $"{assetPath}.meta";
            if (!File.Exists(metaFile))
                return null;

            string line;
            using (var file = new StreamReader(metaFile))
            {
                while ((line = file.ReadLine()) != null)
                {
                    if (!line.StartsWith("guid:", StringComparison.Ordinal))
                        continue;
                    return line.Substring(6);
                }
            }

            return null;
        }

        string ResolveAssetPath(string id)
        {
            if (ResolveAssetPath(id, out var path))
                return path;
            return null;
        }

        public void Update(in string[] updated, in string[] removed, in string[] moved)
        {
            #if NOT_WORKING
            // Postpone changes up to 60 seconds. If nothing changed after 60 seconds,
            // kick off a new dependency database build, when ready, switch the global index db.

            var sw = System.Diagnostics.Stopwatch.StartNew();
            var progressId = Progress.Start($"Updating dependency index ({updated.Length} assets)");

            var affectedGuids = new HashSet<string>();
            ignoredGuids.UnionWith(AssetDatabase.FindAssets("l:Ignore"));

            foreach (var u in updated.Concat(removed).Concat(moved))
            {
                affectedGuids.Add(AssetDatabase.AssetPathToGUID(u));
                foreach(var r in Search($"ref=\"{u}\"", patternMatchLimit: int.MaxValue))
                    affectedGuids.Add(r.id);
                foreach (var r in Search($"from=\"{u}\"", patternMatchLimit: int.MaxValue))
                    affectedGuids.Add(r.id);
            }

            var newIndex = new DependencyIndexer();


            Progress.Finish(progressId);

            UnityEngine.Debug.Log($"Incremental dependency indexing took {sw.Elapsed.TotalMilliseconds,3:0.##} ms");

            UnityEngine.Debug.Log($"Update {string.Join(",", updated)}");
            //UnityEngine.Debug.Log($"Remove {string.Join(",", removed)}");
            //UnityEngine.Debug.Log($"Move {string.Join(",", moved)}");
            UnityEngine.Debug.Log($"Affected GUIDs {string.Join(", ", affectedGuids)}");
            #endif
        }
    }
}
