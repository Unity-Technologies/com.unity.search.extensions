using System;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

namespace UnityEditor.Search
{
	[EditorWindowTitle(title ="Dependency Viewer")]
	class DependencyViewer : EditorWindow, IDependencyViewHost
	{
		static class Styles
		{
			public static GUIStyle lockButton = "IN LockButton";
		}

		[SerializeField] bool m_LockSelection;
		[SerializeField] SplitterInfo m_Splitter;
		[SerializeField] DependencyViewerState m_CurrentState;

		int m_HistoryCursor = -1;
		List<DependencyViewerState> m_History;
		List<DependencyTableView> m_Views;

		[ShortcutManagement.Shortcut("dep_goto_prev_state", typeof(DependencyViewer), KeyCode.LeftArrow, ShortcutManagement.ShortcutModifiers.Alt)]
		internal static void GotoPrev(ShortcutManagement.ShortcutArguments args)
		{
			if (args.context is DependencyViewer viewer)
				viewer.GotoPrevStates();
		}

		[ShortcutManagement.Shortcut("dep_goto_next_state", typeof(DependencyViewer), KeyCode.RightArrow, ShortcutManagement.ShortcutModifiers.Alt)]
		internal static void GotoNext(ShortcutManagement.ShortcutArguments args)
		{
			if (args.context is DependencyViewer viewer)
				viewer.GotoNextStates();
		}

		internal void OnEnable()
		{
			titleContent = new GUIContent("Dependency Viewer", Icons.dependencies);
			m_Splitter = m_Splitter ?? new SplitterInfo(SplitterInfo.Side.Left, 0.1f, 0.9f, this);
			m_CurrentState = m_CurrentState ?? DependencyViewerProviderAttribute.GetDefault().CreateState();
			m_History = new List<DependencyViewerState>();
			m_Splitter.host = this;
			PushViewerState(m_CurrentState);
			UnityEditor.Selection.selectionChanged += OnSelectionChanged;
		}

		internal void OnDisable()
		{
			UnityEditor.Selection.selectionChanged -= OnSelectionChanged;
		}

		internal void OnGUI()
		{
            m_Splitter.Init(position.width / 2.0f);
			var evt = Event.current;

			using (new EditorGUILayout.VerticalScope(GUIStyle.none, GUILayout.ExpandHeight(true)))
			{
				using (new GUILayout.HorizontalScope(Search.Styles.searchReportField))
				{
					EditorGUI.BeginDisabledGroup(m_HistoryCursor <= 0);
					if (GUILayout.Button("<"))
						GotoPrevStates();
					EditorGUI.EndDisabledGroup();
					EditorGUI.BeginDisabledGroup(m_HistoryCursor == m_History.Count - 1);
					if (GUILayout.Button(">"))
						GotoNextStates();
					EditorGUI.EndDisabledGroup();
					GUILayout.Label(m_CurrentState?.description ?? Utils.GUIContentTemp("No selection"), GUILayout.Height(18f));
					GUILayout.FlexibleSpace();

					if (EditorGUILayout.DropdownButton(Utils.GUIContentTemp("Columns"), FocusType.Passive))
						SelectDependencyColumns();

					if (EditorGUILayout.DropdownButton(Utils.GUIContentTemp(m_CurrentState.name), FocusType.Passive))
						OnSourceChange();
					EditorGUI.BeginChangeCheck();

					EditorGUI.BeginDisabledGroup(!m_CurrentState?.trackSelection ?? true);
					m_LockSelection = GUILayout.Toggle(m_LockSelection, GUIContent.none, Styles.lockButton);
					if (EditorGUI.EndChangeCheck() && !m_LockSelection)
						OnSelectionChanged();
					EditorGUI.EndDisabledGroup();
				}

				#if USE_SEARCH_MODULE
				using (SearchMonitor.GetView())
				#endif
				{
					if (m_Views != null && m_Views.Count >= 1)
					{
						EditorGUILayout.BeginHorizontal();
						var treeViewRect = m_Views.Count >= 2 ?
							EditorGUILayout.GetControlRect(false, -1, GUIStyle.none, GUILayout.ExpandHeight(true), GUILayout.Width(Mathf.Ceil(m_Splitter.width - 1))) :
							EditorGUILayout.GetControlRect(false, -1, GUIStyle.none, GUILayout.ExpandHeight(true));
						m_Views[0].OnGUI(treeViewRect);
						if (m_Views.Count >= 2)
						{
							m_Splitter.Draw(evt, treeViewRect);
							treeViewRect = EditorGUILayout.GetControlRect(false, -1, GUIStyle.none, GUILayout.ExpandHeight(true), GUILayout.ExpandWidth(true));
							m_Views[1].OnGUI(treeViewRect);

							if (evt.type == EventType.Repaint)
							{
								GUI.DrawTexture(new Rect(treeViewRect.xMin, treeViewRect.yMin, 1, treeViewRect.height),
													EditorGUIUtility.whiteTexture, ScaleMode.StretchToFill, false, 0f, Color.black, 1f, 0f);
							}
						}

						EditorGUILayout.EndHorizontal();
					}
				}
			}
		}

        private void SelectDependencyColumns()
        {
            var menu = new GenericMenu();
			var columnSetup = DependencyState.defaultColumns;
            menu.AddItem(new GUIContent("Used By"), (columnSetup & DependencyState.DependencyColumns.UsedByRefCount) != 0, () => ToggleColumn(DependencyState.DependencyColumns.UsedByRefCount));
			menu.AddItem(new GUIContent("Path"), (columnSetup & DependencyState.DependencyColumns.Path) != 0, () => ToggleColumn(DependencyState.DependencyColumns.Path));
			menu.AddItem(new GUIContent("Type"), (columnSetup & DependencyState.DependencyColumns.Type) != 0, () => ToggleColumn(DependencyState.DependencyColumns.Type));
			menu.AddItem(new GUIContent("Size"), (columnSetup & DependencyState.DependencyColumns.Size) != 0, () => ToggleColumn(DependencyState.DependencyColumns.Size));
			menu.ShowAsContext();
        }

        private void ToggleColumn(in DependencyState.DependencyColumns dc)
        {
			var columnSetup = DependencyState.defaultColumns;
			if ((columnSetup & dc) != 0)
				columnSetup &= ~dc;
			else
				columnSetup |= dc;
			DependencyState.defaultColumns = columnSetup;
			UpdateSelection();
        }

        public void PushViewerState(DependencyViewerState state)
		{
			if (state == null)
				return;
			SetViewerState(state);
			if (m_CurrentState.states.Count != 0)
			{
				m_History.Add(m_CurrentState);
				m_HistoryCursor = m_History.Count - 1;
			}
		}

		List<DependencyTableView> BuildViews(DependencyViewerState state)
		{
			return state.states.Select(s => new DependencyTableView(s, this)).ToList();
		}

		void OnSelectionChanged()
		{
			if (UnityEditor.Selection.objects.Length == 0 || m_LockSelection || !m_CurrentState.trackSelection)
				return;
			UpdateSelection();
		}

        void UpdateSelection()
        {
            PushViewerState(m_CurrentState.provider.CreateState());
            Repaint();
        }

        void SetViewerState(DependencyViewerState state)
		{
			m_CurrentState = state;
			m_Views = BuildViews(m_CurrentState);
			titleContent = m_CurrentState.windowTitle;
		}

		void OnSourceChange()
		{			
			var menu = new GenericMenu();
			foreach(var stateProvider in DependencyViewerProviderAttribute.providers.Where(s => s.flags.HasFlag(DependencyViewerFlags.TrackSelection)))
				menu.AddItem(new GUIContent(stateProvider.name), false, () => PushViewerState(stateProvider.CreateState()));
			menu.AddSeparator("");
			foreach (var stateProvider in DependencyViewerProviderAttribute.providers.Where(s => !s.flags.HasFlag(DependencyViewerFlags.TrackSelection)))
				menu.AddItem(new GUIContent(stateProvider.name), false, () => PushViewerState(stateProvider.CreateState()));

			menu.AddSeparator("");

			var depQueries = SearchQueryAsset.savedQueries.Where(sq => AssetDatabase.GetLabels(sq).Any(l => l.ToLowerInvariant() == "dependencies")).ToArray();
			if (depQueries.Length > 0)
			{
				foreach (var sq in depQueries)
				{
					menu.AddItem(new GUIContent(sq.name, sq.description), false, () => PushViewerState(DependencyBuiltinStates.CreateStateFromQuery(sq)));
				}
				menu.AddSeparator("");
			}
			
			menu.AddItem(new GUIContent("Build"), false, () => Dependency.Build());
			menu.ShowAsContext();
		}				

		void GotoNextStates()
		{
			SetViewerState(m_History[++m_HistoryCursor]);
			Repaint();
		}

		void GotoPrevStates()
		{
			SetViewerState(m_History[--m_HistoryCursor]);
			Repaint();
		}

		void ResizeColumns()
        {
			foreach (var v in m_Views)
				v.ResizeColumns();
        }
	}
}
