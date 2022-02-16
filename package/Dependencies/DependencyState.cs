using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnityEditor.Search
{
    [Serializable]
    class DependencyState : ISerializationCallbackReceiver, IDisposable
    {        
        public string name;

        #if UNITY_2022_2_OR_NEWER
        public bool supportsDepth;
        #endif

        [NonSerialized] private SearchTable m_TableConfig;
        [SerializeField] private SearchViewState m_ViewState;

        public string guid => m_ViewState.sessionId;
        public SearchTable tableConfig => m_TableConfig;
        public SearchContext context => m_ViewState.context;

        public DependencyState(string name, SearchContext context)
            : this(name, context, CreateDefaultTable(name))
        {
        }

        #if USE_SEARCH_EXTENSION_API

        public DependencyState(ISearchQuery query)
        {
            name = query.GetName();
            m_ViewState = query.GetViewState() ?? throw new ArgumentNullException(nameof(query), "Invalid search view state");
            m_TableConfig = query.GetSearchTable() == null || query.GetSearchTable().columns.Length == 0 ? CreateDefaultTable(query.GetName()) : query.GetSearchTable();
        }

        #else

        public DependencyState(SearchQuery query)
        {
            name = query.name;
            m_ViewState = query.viewState ?? throw new ArgumentNullException(nameof(query), "Invalid search view state");
            m_TableConfig = query.tableConfig == null || query.tableConfig.columns.Length == 0 ? CreateDefaultTable(query.name) : query.tableConfig;
        }

        public DependencyState(SearchQueryAsset query)
            : this(query.ToSearchQuery())
        {
        }

        #endif

        public DependencyState(string name, SearchContext context, SearchTable tableConfig)
        {
            this.name = name;
            m_TableConfig = tableConfig;
            m_ViewState = new SearchViewState(context, tableConfig);
        }

        public void Dispose()
        {
            m_ViewState.context?.Dispose();
        }

        public void OnBeforeSerialize()
        {
        }

        public void OnAfterDeserialize()
        {
            if (m_TableConfig == null)
                m_TableConfig = m_ViewState.tableConfig;
            m_TableConfig?.InitFunctors();
        }

        static SearchTable CreateDefaultTable(string tableName)
        {
            return new SearchTable(Guid.NewGuid().ToString("N"), tableName, GetDefaultColumns(tableName));
        }

        [Flags]
        public enum Columns
        {
            None = 0,
            UsedByRefCount = 1 << 0,
            Path = 1 << 1,
            Type = 1 << 2,
            Size = 1 << 3,
            RuntimeSize = 1 << 4,
            Depth = 1 << 5,

            All = UsedByRefCount | Path | Type | Size | RuntimeSize,
            Default = UsedByRefCount | Path
        }

        public static Columns defaultColumns
        {
            get => (Columns)EditorPrefs.GetInt("DependencyColumns", (int)Columns.Default);
            set => EditorPrefs.SetInt("DependencyColumns", (int)value);
        }

        static IEnumerable<SearchColumn> GetDefaultColumns(string tableName)
        {
            var defaultDepFlags = SearchColumnFlags.CanSort;
            var columnSetup = defaultColumns;
            if ((columnSetup & Columns.UsedByRefCount) != 0)
                yield return new SearchColumn("@", "refCount", new GUIContent("@", null, L10n.Tr("The used by reference count.")), defaultDepFlags | SearchColumnFlags.TextAlignmentRight) { width = 30 };
            if ((columnSetup & Columns.Path) != 0)
                yield return new SearchColumn(L10n.Tr(tableName), "label", "path", new GUIContent(L10n.Tr(tableName), null, L10n.Tr("The project file path of the dependency object.")), defaultDepFlags);
            if ((columnSetup & Columns.Type) != 0)
                yield return new SearchColumn(L10n.Tr("Type"), "type", new GUIContent(L10n.Tr("Type"), null, L10n.Tr("The type of the dependency object.")), defaultDepFlags | SearchColumnFlags.Hidden) { width = 80 };
            if ((columnSetup & Columns.Size) != 0)
                yield return new SearchColumn(L10n.Tr("File Size"), "size", "size", new GUIContent(L10n.Tr("File Size"), null, L10n.Tr("The file size of the dependency object.")), defaultDepFlags);
            if ((columnSetup & Columns.RuntimeSize) != 0)
                yield return new SearchColumn(L10n.Tr("Runtime Size"), "gsize", "size", new GUIContent(L10n.Tr("Runtime Size"), null, L10n.Tr("The runtime size of the object.")), defaultDepFlags);
            if ((columnSetup & Columns.Depth) != 0)
                yield return new SearchColumn(L10n.Tr("Depth"), Dependency.refDepthField, Dependency.refDepthColumnFormat, new GUIContent(L10n.Tr("Depth"), null, L10n.Tr("Depth relative to object of interest.")), defaultDepFlags);
        }
    }
}
