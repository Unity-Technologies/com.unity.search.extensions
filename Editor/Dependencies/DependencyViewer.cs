#if USE_SEARCH_TABLE
using System.Linq;
using System.Collections.Generic;
using UnityEngine;

namespace UnityEditor.Search
{
    [EditorWindowTitle(title = "Dependency Viewer")]
    class DependencyViewer : EditorWindow, IDependencyViewHost
    {
        static class Styles
        {
            public static GUIStyle lockButton = "IN LockButton";
            public static GUIStyle objectLink = new GUIStyle(EditorStyles.linkLabel)
            {
                richText = true,
                wordWrap = true,
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(0, 0, 0, 0),
                margin = new RectOffset(0, 0, 0, 0)
            };

            public static Texture2D sceneIcon = Utils.LoadIcon("SceneAsset Icon");
            public static GUIContent sceneRefs = new GUIContent("Scene", sceneIcon);
        }

        [SerializeField] bool m_LockSelection;
        [SerializeField] SplitterInfo m_Splitter;
        [SerializeField] DependencyViewerState m_CurrentState;
        [SerializeField] bool m_ShowSceneRefs = true;
        [SerializeField] int m_DependencyDepthLevel = 1;

        const int k_MaxHistorySize = 10;
        int m_HistoryCursor = -1;
        List<DependencyViewerState> m_History;
        List<DependencyTableView> m_Views;

        public bool showDepthSlider => m_Views.Any(view => view.state.supportsDepth);
        public bool showSceneRefs => m_ShowSceneRefs;

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
            titleContent = new GUIContent("Dependency Viewer", Icons.quicksearch);
            m_Splitter = m_Splitter ?? new SplitterInfo(SplitterInfo.Side.Left, 0.1f, 0.9f, this);
            m_CurrentState = m_CurrentState ?? DependencyViewerProviderAttribute.GetDefault().CreateState(GetViewerFlags());
            m_History = new List<DependencyViewerState>();
            m_HistoryCursor = -1;
            m_Splitter.host = this;
            PushViewerState(m_CurrentState);
            Selection.selectionChanged += OnSelectionChanged;
            Dependency.indexingFinished += OnIndexingFinished;

            DependencyProject.Init();
        }

        internal void OnDisable()
        {
            Selection.selectionChanged -= OnSelectionChanged;
            Dependency.indexingFinished -= OnIndexingFinished;
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
                    if (GUILayout.Button("<", EditorStyles.miniButton, GUILayout.MaxWidth(20)))
                        GotoPrevStates();
                    EditorGUI.EndDisabledGroup();
                    EditorGUI.BeginDisabledGroup(m_HistoryCursor == m_History.Count - 1);
                    if (GUILayout.Button(">", EditorStyles.miniButton, GUILayout.MaxWidth(20)))
                        GotoNextStates();
                    EditorGUI.EndDisabledGroup();
                    var assetLink = m_CurrentState?.description ?? Utils.GUIContentTemp("No selection");
                    if (assetLink.text.IndexOf('/') != -1)
                        assetLink.text = GetName(assetLink.text);
                    const int maxTitleLength = 89;
                    if (assetLink.text.Length > maxTitleLength)
                        assetLink.text = "..." + assetLink.text.Replace("<b>", "").Replace("</b>", "").Substring(assetLink.text.Length - maxTitleLength);
                    if (GUILayout.Button(assetLink, Styles.objectLink, GUILayout.Height(18f), GUILayout.ExpandWidth(true)))
                        m_CurrentState.Ping();
                    GUILayout.FlexibleSpace();

                    if (showDepthSlider)
                    {
                        EditorGUI.BeginChangeCheck();
                        var oldLabelWidth = EditorGUIUtility.labelWidth;
                        EditorGUIUtility.labelWidth = 40;
                        m_DependencyDepthLevel = EditorGUILayout.IntSlider(new GUIContent("Depth"), m_DependencyDepthLevel, 1, 5);
                        EditorGUIUtility.labelWidth = oldLabelWidth;
                        if (EditorGUI.EndChangeCheck())
                        {
                            DependencyViewerFlags flags = DependencyViewerFlags.Uses;
                            var items = m_Views[0].GetElements();
                            /*
                            DependencyTableUtilities.ExpandDependencies(flags, items, m_DependencyDepthLevel, 
                                (items, depth) => { }, items => {
                                    m_Views[0].table.AddItems(items);
                            });
                            */
                        }
                    }

                    var old = m_ShowSceneRefs;
                    GUILayout.Label(Styles.sceneRefs, GUILayout.Height(18f), GUILayout.Width(65f));
                    m_ShowSceneRefs = EditorGUILayout.Toggle(m_ShowSceneRefs, GUILayout.Width(20f));
                    if (old != m_ShowSceneRefs)
                        RefreshState();

                    if (GUILayout.Button("Build", EditorStyles.miniButton))
                        Dependency.Build();

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
                        var multiView = m_Views.Count == 2;
                        var treeViewRect = multiView ?
                            EditorGUILayout.GetControlRect(false, -1, GUIStyle.none, GUILayout.ExpandHeight(true), GUILayout.Width(Mathf.Ceil(m_Splitter.width - 1))) :
                            EditorGUILayout.GetControlRect(false, -1, GUIStyle.none, GUILayout.ExpandHeight(true));
                        m_Views[0].OnGUI(treeViewRect);
                        if (multiView)
                        {
                            m_Splitter.Draw(evt, treeViewRect);
                            treeViewRect = EditorGUILayout.GetControlRect(false, -1, GUIStyle.none, GUILayout.ExpandHeight(true), GUILayout.ExpandWidth(true));
                            m_Views[1].OnGUI(treeViewRect);

                            if (evt.type == EventType.Repaint)
                            {
                                GUI.DrawTexture(new Rect(treeViewRect.xMin, treeViewRect.yMin, 1, treeViewRect.height),
                                                    EditorGUIUtility.whiteTexture, ScaleMode.StretchToFill, false, 0f, new Color(35/255f, 35 / 255f, 35 / 255f), 1f, 0f);
                            }
                        }

                        EditorGUILayout.EndHorizontal();
                    }
                }
            }
        }

        private static string GetName(in string n)
        {
            var p = n.LastIndexOf('/');
            if (p == -1)
                return n;
            return n.Substring(p+1);
        }

        private void SelectDependencyColumns()
        {
            var menu = new GenericMenu();
            SelectDependencyColumns(menu);
            menu.ShowAsContext();
        }

        public void SelectDependencyColumns(GenericMenu menu, in string prefix = "")
        {
            var columnSetup = DependencyState.defaultColumns;
            menu.AddItem(new GUIContent($"{prefix}Ref. Count"), (columnSetup & DependencyState.Columns.UsedByRefCount) != 0, () => ToggleColumn(DependencyState.Columns.UsedByRefCount));
            menu.AddItem(new GUIContent($"{prefix}Path"), (columnSetup & DependencyState.Columns.Path) != 0, () => ToggleColumn(DependencyState.Columns.Path));
            menu.AddItem(new GUIContent($"{prefix}Type"), (columnSetup & DependencyState.Columns.Type) != 0, () => ToggleColumn(DependencyState.Columns.Type));
            menu.AddItem(new GUIContent($"{prefix}File Size"), (columnSetup & DependencyState.Columns.Size) != 0, () => ToggleColumn(DependencyState.Columns.Size));
            menu.AddItem(new GUIContent($"{prefix}Runtime Size"), (columnSetup & DependencyState.Columns.RuntimeSize) != 0, () => ToggleColumn(DependencyState.Columns.RuntimeSize));
            menu.AddItem(new GUIContent($"{prefix}Depth"), (columnSetup & DependencyState.Columns.Depth) != 0, () => ToggleColumn(DependencyState.Columns.Depth));
        }

        public void ToggleColumn(in DependencyState.Columns dc)
        {
            var columnSetup = DependencyState.defaultColumns;
            if ((columnSetup & dc) != 0)
                columnSetup &= ~dc;
            else
                columnSetup |= dc;
            if (columnSetup == 0)
                columnSetup = DependencyState.Columns.Path;
            DependencyState.defaultColumns = columnSetup;
            RefreshState();
        }

        public void PushViewerState(DependencyViewerState state)
        {
            if (state == null)
                return;
            SetViewerState(state);
            if (m_CurrentState.states.Count != 0)
            {
                if (m_HistoryCursor != -1 && m_HistoryCursor <= m_History.Count - 1)
                {
                    m_History.RemoveRange(m_HistoryCursor + 1, m_History.Count - (m_HistoryCursor + 1));
                }

                m_History.Add(m_CurrentState);
                if (m_History.Count > k_MaxHistorySize)
                {
                    m_History.RemoveAt(0);
                }
                m_HistoryCursor = m_History.Count - 1;
            }
        }

        List<DependencyTableView> BuildViews(DependencyViewerState state)
        {
            return state.states.Select(s => new DependencyTableView(s, this)).ToList();
        }

        void OnIndexingFinished()
        {
            RefreshState();
        }

        void OnSelectionChanged()
        {
            if (Selection.objects.Length == 0 || m_LockSelection || !m_CurrentState.trackSelection)
                return;
            UpdateSelection();
        }

        void UpdateSelection()
        {
            PushViewerState(m_CurrentState.provider.CreateState(GetViewerFlags()));
            Repaint();
        }

        void RefreshState()
        {
            SetViewerState(m_CurrentState.provider.CreateState(GetViewerFlags()));
            Repaint();
        }

        void SetViewerState(DependencyViewerState state)
        {
            m_DependencyDepthLevel = 1;
            m_CurrentState = state;
            m_Views = BuildViews(m_CurrentState);
            titleContent = m_CurrentState.windowTitle;
        }

        DependencyViewerFlags GetViewerFlags()
        {
            if (m_ShowSceneRefs)
                return DependencyViewerFlags.ShowSceneRefs;
            return DependencyViewerFlags.None;
        }

        void OnSourceChange()
        {
            var menu = new GenericMenu();
            foreach (var stateProvider in DependencyViewerProviderAttribute.providers.Where(s => s.flags.HasFlag(DependencyViewerFlags.TrackSelection)))
                menu.AddItem(new GUIContent(stateProvider.name), false, () => PushViewerState(stateProvider.CreateState(GetViewerFlags())));
            menu.AddSeparator("");
            foreach (var stateProvider in DependencyViewerProviderAttribute.providers.Where(s => !s.flags.HasFlag(DependencyViewerFlags.TrackSelection)))
                menu.AddItem(new GUIContent(stateProvider.name), false, () => PushViewerState(stateProvider.CreateState(GetViewerFlags())));

            #if !UNITY_2021
            var depQueries = SearchQueryAsset.savedQueries.Where(sq => AssetDatabase.GetLabels(sq).Any(l => l.ToLowerInvariant() == "dependencies")).ToArray();
            if (depQueries.Length > 0)
            {
                menu.AddSeparator("");
                foreach (var sq in depQueries)
                {
                    menu.AddItem(new GUIContent(sq.name, sq.description), false, () => PushViewerState(DependencyBuiltinStates.CreateStateFromQuery(sq)));
                }
            }
            #endif

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
#endif
