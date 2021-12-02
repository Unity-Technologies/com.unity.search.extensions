#if !USE_SEARCH_DEPENDENCY_VIEWER || USE_SEARCH_MODULE
using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnityEditor.Search
{
    [Serializable]
    class DependencyState : ISerializationCallbackReceiver, IDisposable
    {
        [NonSerialized] private SearchTable m_TableConfig;

        public string guid => m_Query.guid;
        public SearchTable tableConfig => m_TableConfig;
        public bool supportsDepth;

        public DependencyState(string name, SearchContext context)
            : this(name, context, CreateDefaultTable(name))
        {
        }

        #if USE_SEARCH_EXTENSION_API
        [SerializeField] private ISearchQuery m_Query;
        public SearchContext context => m_Query.GetViewState().context;
        public DependencyState(ISearchQuery query)
        {
            m_Query = query;
            m_TableConfig = query.GetTableConfig() == null || query.GetTableConfig().columns.Length == 0 ? CreateDefaultTable(query.GetName()) : query.GetTableConfig();
        }

        public DependencyState(string name, SearchContext context, SearchTable tableConfig)
        {
            m_TableConfig = tableConfig;
            m_Query = SearchUtils.CreateQuery(name, context, m_TableConfig);
        }
        public static SearchViewState GetViewState(in ISearchQuery query) => query.GetViewState();
        public static SearchTable GetTableConfig(in ISearchQuery query) => query.GetTableConfig();
        #else
        [SerializeField] private SearchQuery m_Query;
        public SearchContext context => m_Query.viewState.context;

        public DependencyState(SearchQuery query)
        {
            m_Query = query;
            m_TableConfig = query.tableConfig == null || query.tableConfig.columns.Length == 0 ? CreateDefaultTable(query.name) : query.tableConfig;
        }

        public DependencyState(SearchQueryAsset query)
            : this(query.ToSearchQuery())
        {
        }

        public DependencyState(string name, SearchContext context, SearchTable tableConfig)
        {
            m_TableConfig = tableConfig;
            m_Query = new SearchQuery()
            {
                name = name,
                viewState = new SearchViewState(context),
                displayName = name,
                tableConfig = m_TableConfig
            };
        }

        public static SearchViewState GetViewState(in SearchQuery query) => query.viewState;
        public static SearchTable GetTableConfig(in SearchQuery query) => query.tableConfig;
        #endif

        public void Dispose()
        {
            GetViewState(m_Query)?.context?.Dispose();
        }

        public void OnBeforeSerialize()
        {
        }

        public void OnAfterDeserialize()
        {
            if (m_TableConfig == null)
                m_TableConfig = GetTableConfig(m_Query);
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
#endif
