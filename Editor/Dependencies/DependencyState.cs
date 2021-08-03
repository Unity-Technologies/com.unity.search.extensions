using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnityEditor.Search
{
	[Serializable]
	class DependencyState : ISerializationCallbackReceiver, IDisposable
	{
		[SerializeField] private SearchQuery m_Query;
		[NonSerialized] private SearchTable m_TableConfig;

		public string guid => m_Query.guid;
		public SearchContext context => m_Query.viewState.context;
		public SearchTable tableConfig => m_TableConfig;

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

		public DependencyState(string name, SearchContext context)
			: this(name, context, CreateDefaultTable(name))
		{
		}

		public void Dispose()
		{
			m_Query.viewState?.context?.Dispose();
		}

		public void OnBeforeSerialize()
		{
		}

		public void OnAfterDeserialize()
		{
			if (m_TableConfig == null)
				m_TableConfig = m_Query.tableConfig;
			m_TableConfig?.InitFunctors();
		}

		static SearchTable CreateDefaultTable(string tableName)
		{
			return new SearchTable(Guid.NewGuid().ToString("N"), tableName, GetDefaultColumns(tableName));
		}

		[Flags]
		public enum DependencyColumns
        {
			None = 0,
			UsedByRefCount = 1 << 0,
			Path = 1 << 1,
			Type = 1 << 2,
			Size = 1 << 3,

			All = UsedByRefCount | Path | Type | Size
		}

		public static DependencyColumns defaultColumns
        {
			get => (DependencyColumns)EditorPrefs.GetInt("DependencyColumns", (int)DependencyColumns.All);
			set => EditorPrefs.SetInt("DependencyColumns", (int)value);
        }

		static IEnumerable<SearchColumn> GetDefaultColumns(string tableName)
		{
			var defaultDepFlags = SearchColumnFlags.CanSort;
			var columnSetup = defaultColumns;
			if ((columnSetup & DependencyColumns.UsedByRefCount) != 0)
				yield return new SearchColumn("@", "refCount", null, defaultDepFlags | SearchColumnFlags.TextAlignmentRight) { width = 30 };
			if ((columnSetup & DependencyColumns.Path) != 0)
				yield return new SearchColumn(tableName, "label", "path", null, defaultDepFlags);
			if ((columnSetup & DependencyColumns.Type) != 0)
				yield return new SearchColumn("Type", "type", null, defaultDepFlags | SearchColumnFlags.Hidden) { width = 80 };
			if ((columnSetup & DependencyColumns.Size) != 0)
				yield return new SearchColumn("Size", "size", "size", null, defaultDepFlags);
		}
	}
}
