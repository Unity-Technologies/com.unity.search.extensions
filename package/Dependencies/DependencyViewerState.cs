using System;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

#pragma warning disable UNT0007 // Null coalescing on Unity objects

namespace UnityEditor.Search
{
    [Flags]
    enum DependencyViewerFlags
    {
        None = 0,
        Uses = 1 << 1,
        UsedBy = 1 << 2,
        TrackSelection = 1 << 3,
        ShowSceneRefs = 1 << 4,
        All = Uses | UsedBy
    }

    struct IdInfo
    {
        public string globalId;
        public string path;
        public int instanceID;
        public bool isAssetId;
    }

    [Serializable]
    struct DependencyViewerConfig
    {
        public DependencyViewerConfig(DependencyViewerFlags flags, int depthLevel = 1)
        {
            this.flags = flags;
            #if UNITY_2022_2_OR_NEWER
            this.depthLevel = depthLevel;
            #endif
        }

        public DependencyViewerFlags flags;
        #if UNITY_2022_2_OR_NEWER
        public int depthLevel;
        #endif
    }

    [Serializable]
    class DependencyViewerState
    {
        public string name;
        public List<string> globalIds;
        public DependencyViewerConfig config;
        public List<DependencyState> states;

        [SerializeField] internal int viewerProviderId;
        [SerializeField] private GUIContent m_Description;
        [SerializeField] private GUIContent m_WindowTitle;

        public DependencyViewerProviderAttribute provider =>
            DependencyViewerProviderAttribute.GetProvider(viewerProviderId)
            ?? DependencyViewerProviderAttribute.GetDefault();

        public bool trackSelection => config.flags.HasFlag(DependencyViewerFlags.TrackSelection);

        public GUIContent description
        {
            get
            {
                if (m_Description != null)
                    return m_Description;

                if (globalIds != null)
                {
                    var names = Dependency.EnumeratePaths(globalIds).ToList();
                    if (names.Count == 0)
                        m_Description = new GUIContent("No dependencies");
                    else if (names.Count == 1)
                        m_Description = new GUIContent(string.Join(", ", names), GetPreview());
                    else if (names.Count < 4)
                        m_Description = new GUIContent(string.Join(", ", names), EditorGUIUtility.FindTexture("Search Icon"));
                    else
                        m_Description = new GUIContent($"{names.Count} object selected", string.Join("\n", names));
                }
                else
                {
                    m_Description = new GUIContent(name);
                }
                return m_Description;
            }

            set => m_Description = value;
        }

        public GUIContent windowTitle
        {
            get
            {
                if (m_WindowTitle != null)
                    return m_WindowTitle;

                if (globalIds != null)
                {
                    var names = Dependency.EnumeratePaths(globalIds).ToList();
                    if (names.Count != 1)
                        m_WindowTitle = new GUIContent($"Dependency Viewer ({names.Count})", GetDefaultIcon());
                    else
                        m_WindowTitle = new GUIContent(System.IO.Path.GetFileNameWithoutExtension(names.First()), GetIcon());
                }
                else
                {
                    m_WindowTitle = new GUIContent(name);
                }

                return m_WindowTitle;
            }

            set => m_WindowTitle = value;
        }

        public DependencyViewerState(string name, DependencyState state)
                : this(name, null, new[] { state })
        {
        }

        public DependencyViewerState(string name, IEnumerable<DependencyState> states = null)
                : this(name, null, states)
        {
        }

        public DependencyViewerState(string name, IEnumerable<string> globalIds, IEnumerable<DependencyState> states = null)
        {
            this.name = name;
            this.globalIds = globalIds == null ? null : globalIds.ToList();
            this.states = states != null ? states.ToList() : new List<DependencyState>();
            viewerProviderId = -1;
            config = new DependencyViewerConfig(DependencyViewerFlags.TrackSelection);
        }

        internal void Ping()
        {
            if (globalIds == null || globalIds.Count == 0 || !GlobalObjectId.TryParse(globalIds[0], out var gid))
                return;
            var obj = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(gid);
            EditorGUIUtility.PingObject(obj);
        }

        Texture GetIcon()
        {
            if (globalIds == null || globalIds.Count == 0 || !GlobalObjectId.TryParse(globalIds[0], out var gid))
                return GetDefaultIcon();
            return AssetDatabase.GetCachedIcon(AssetDatabase.GUIDToAssetPath(gid.assetGUID)) ?? GetDefaultIcon();
        }

        Texture GetPreview()
        {
            if (globalIds == null || globalIds.Count == 0 || !GlobalObjectId.TryParse(globalIds[0], out var gid))
                return GetDefaultIcon();
            var obj = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(gid);
            return AssetPreview.GetAssetPreview(obj) ?? GetDefaultIcon();
        }

        static Texture GetDefaultIcon()
        {
            return EditorGUIUtility.FindTexture("Search Icon");
        }

        IEnumerable<string> EnumeratePaths()
        {
            if (globalIds == null || globalIds.Count == 0)
                yield break;

            foreach (var sgid in globalIds)
            {
                if (!GlobalObjectId.TryParse(sgid, out var gid))
                    continue;
                var instanceId = GlobalObjectId.GlobalObjectIdentifierToInstanceIDSlow(gid);
                var assetPath = AssetDatabase.GetAssetPath(instanceId);
                if (!string.IsNullOrEmpty(assetPath))
                    yield return assetPath;
                else if (EditorUtility.InstanceIDToObject(instanceId) is UnityEngine.Object obj)
                    yield return SearchUtils.GetObjectPath(obj).Substring(1);
            }
        }
    }
}