#if !UNITY_2021_1
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

		#if !UNITY_2021
		public DependencyState(SearchQueryAsset query)
			: this(query.ToSearchQuery())
		{
		}
		#endif

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
		public enum Columns
        {
			None = 0,
			UsedByRefCount = 1 << 0,
			Path = 1 << 1,
			Type = 1 << 2,
			Size = 1 << 3,

			All = UsedByRefCount | Path | Type | Size
		}

		public static Columns defaultColumns
        {
			get => (Columns)EditorPrefs.GetInt("DependencyColumns", (int)Columns.All);
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
				yield return new SearchColumn(L10n.Tr("Size"), "size", "size", new GUIContent(L10n.Tr("Size"), null, L10n.Tr("The file size of the dependency object.")), defaultDepFlags);
		}
	}
}
#endif
