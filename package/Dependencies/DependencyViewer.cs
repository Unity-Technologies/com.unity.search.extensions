using System.Linq;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_2023_1_OR_NEWER
using UnityEngine.UIElements;
using UnityEditor.UIElements;
#endif


namespace UnityEditor.Search
{
    [EditorWindowTitle(title = "Dependency Viewer")]
    public class DependencyViewer : EditorWindow, IDependencyViewHost
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

            public static readonly GUIStyle searchTabBackground = new GUIStyle() { name = "quick-search-tab-background" };

            public static readonly GUIStyle toolbar = new GUIStyle("Toolbar")
            {
                name = "quick-search-bar",
                margin = new RectOffset(0, 0, 0, 0),
                padding = new RectOffset(4, 8, 4, 4),
                border = new RectOffset(0, 0, 0, 0),
                fixedHeight = 0f
            };

            public static readonly GUIStyle searchReportField = new GUIStyle(searchTabBackground)
            {
                padding = toolbar.padding
            };

            public static Texture2D sceneIcon = EditorGUIUtility.TrIconContent("SceneAsset Icon").image as Texture2D;
            public static GUIContent sceneRefs = new GUIContent("Scene", sceneIcon);
            public static GUIContent depthContent = new GUIContent("Depth");
            public const int minDepth = 1;
            public const int maxDepth = 10;
        }

        [SerializeField] bool m_LockSelection;
        [SerializeField] DependencyViewSplitterInfo m_Splitter;
        [SerializeField] DependencyViewerState m_CurrentState;
        [SerializeField] bool m_ShowSceneRefs = true;
#if UNITY_2022_2_OR_NEWER
        [SerializeField] int m_DependencyDepthLevel = 1;
#endif

        private const string k_SplitterViewDataKey = "dep-viewer-splitter__view-data-key";
        private const string k_SplitterViewPrefKey = "dep-viewer-splitter-pref-key";

        const int k_MaxHistorySize = 10;
        int m_HistoryCursor = -1;
        List<DependencyViewerState> m_History;
        List<BaseDependencyTableView> m_Views;

#if UNITY_2022_2_OR_NEWER
        public bool showDepthSlider => m_Views?.Any(view => view?.state?.supportsDepth ?? false) ?? false;
#endif
        public bool showSceneRefs => m_ShowSceneRefs;
        public bool hasIndex { get; set; }
        public bool wantsRebuild { get; set; }
        public bool isReady { get; set; }
        public bool hasUpdates { get; set; }

        #if UNITY_2023_1_OR_NEWER
        public VisualElement m_TableViewContainer;
        public TwoPaneSplitView m_TwoPaneSplitter;
        #endif

        #region Shortcut Definitions
        [ShortcutManagement.Shortcut("dep_refresh_state", typeof(DependencyViewer), KeyCode.F5)]
        internal static void RefreshState(ShortcutManagement.ShortcutArguments args)
        {
            if (args.context is DependencyViewer viewer)
                viewer.RefreshState();
        }

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
        #endregion

        public bool IsReady()
        {
            return hasIndex && isReady && (m_Views?.All(v => !v.state.context.searchInProgress) ?? false);
        }

        public IEnumerable<string> GetUses()
        {
            return GetViewItemsIds(0);
        }

        public IEnumerable<string> GetUsedBy()
        {
            return GetViewItemsIds(1);
        }

        public IEnumerable<string> GetViewItemsIds(int viewIndex)
        {
            if (viewIndex < 0 || viewIndex >= m_Views.Count)
                yield break;

            foreach (var e in m_Views[viewIndex].GetElements())
            {
                if (e == null)
                    continue;
                yield return e.id;
            }
        }

        internal void OnEnable()
        {
#if UNITY_2022_2_OR_NEWER
            m_DependencyDepthLevel = m_DependencyDepthLevel <= 0 ? 1 : m_DependencyDepthLevel;
#endif

            titleContent = new GUIContent("Dependency Viewer", EditorGUIUtility.FindTexture("Search Icon"));
            m_Splitter = m_Splitter ?? new DependencyViewSplitterInfo(DependencyViewSplitterInfo.Side.Left, 0.1f, 0.9f, this);
            m_CurrentState = m_CurrentState ?? DependencyViewerProviderAttribute.GetDefault().CreateState(GetConfig());
            m_History = new List<DependencyViewerState>();
            m_HistoryCursor = -1;
            m_Splitter.host = this;
            SetNewDependencyStateSource(m_CurrentState);
            Selection.selectionChanged += OnSelectionChanged;
            Dependency.indexingFinished += OnIndexingFinished;

            DependencyProject.Init();
            UpdateSelection();
        }

        internal void OnDisable()
        {
            Selection.selectionChanged -= OnSelectionChanged;
            Dependency.indexingFinished -= OnIndexingFinished;
        }

#if UNITY_2023_1_OR_NEWER
        internal void CreateGUI()
        {
            var toolBar = new IMGUIContainer(() =>
            {
                var evt = Event.current;
                InitUIState(evt);
                DrawToolbar(evt);
            });

            // Splitter, TableView.
            rootVisualElement.Add(toolBar);

            m_TableViewContainer = new VisualElement();
            SearchElement.AppendStyleSheets(m_TableViewContainer);
            m_TableViewContainer.style.flexDirection = FlexDirection.Row;
            m_TableViewContainer.style.flexGrow = 1;
            rootVisualElement.Add(m_TableViewContainer);
            AddViewsToContainer();
        }
#else
        internal void OnGUI()
        {
            var evt = Event.current;
            DrawGUI(evt);
        }
#endif

        #region IMGUI 22.3 and under
        void DrawGUI(Event evt)
        {
            m_Splitter.Init(position.width / 2.0f);
            using (new EditorGUILayout.VerticalScope(GUIStyle.none, GUILayout.ExpandHeight(true)))
            {
                DrawToolbar(evt);
                DrawDependencyTableViews(evt);
            }
        }

        void InitUIState(Event evt)
        {
            if (evt.type == EventType.Layout)
            {
                isReady = Dependency.IsReady();
                hasIndex = Dependency.HasIndex();
                hasUpdates = Dependency.HasUpdate();
                wantsRebuild = evt.control && evt.shift;
            }
        }

        void DrawToolbar(Event evt)
        {
            using (new GUILayout.HorizontalScope(Styles.searchReportField))
            {
                EditorGUI.BeginDisabledGroup(m_HistoryCursor <= 0);
                if (GUILayout.Button("<", EditorStyles.miniButton, GUILayout.MaxWidth(20)))
                    GotoPrevStates();
                EditorGUI.EndDisabledGroup();
                EditorGUI.BeginDisabledGroup(m_HistoryCursor == m_History.Count - 1);
                if (GUILayout.Button(">", EditorStyles.miniButton, GUILayout.MaxWidth(20)))
                    GotoNextStates();
                EditorGUI.EndDisabledGroup();
                var assetLink = m_CurrentState?.description ?? EditorGUIUtility.TrTempContent("No selection");
                if (assetLink.text.IndexOf('/') != -1)
                    assetLink.text = GetName(assetLink.text);
                const int maxTitleLength = 89;
                if (assetLink.text.Length > maxTitleLength)
                    assetLink.text = "..." + assetLink.text.Replace("<b>", "").Replace("</b>", "").Substring(assetLink.text.Length - maxTitleLength);
                if (GUILayout.Button(assetLink, Styles.objectLink, GUILayout.Height(18f), GUILayout.ExpandWidth(true)))
                {
                    m_CurrentState.Ping();
                }
                GUILayout.FlexibleSpace();

#if UNITY_2022_2_OR_NEWER
                if (showDepthSlider)
                {
                    EditorGUI.BeginChangeCheck();
                    var oldLabelWidth = EditorGUIUtility.labelWidth;
                    EditorGUIUtility.labelWidth = 40;
                    m_DependencyDepthLevel = EditorGUILayout.IntSlider(Styles.depthContent, m_DependencyDepthLevel, Styles.minDepth, Styles.maxDepth);
                    EditorGUIUtility.labelWidth = oldLabelWidth;
                    if (EditorGUI.EndChangeCheck())
                    {
                        RefreshState();
                    }
                }
#endif
                var old = m_ShowSceneRefs;
                GUILayout.Label(Styles.sceneRefs, GUILayout.Height(18f), GUILayout.Width(65f));
                m_ShowSceneRefs = EditorGUILayout.Toggle(m_ShowSceneRefs, GUILayout.Width(20f));
                if (old != m_ShowSceneRefs)
                    RefreshState();

                if (!hasIndex || wantsRebuild)
                {
                    if (GUILayout.Button("Build", EditorStyles.miniButton))
                        Dependency.Build();
                }
                else if (hasUpdates && isReady)
                {
                    if (GUILayout.Button("Update", EditorStyles.miniButton))
                        Dependency.Update(Repaint);
                }

                if (EditorGUILayout.DropdownButton(EditorGUIUtility.TrTempContent("Columns"), FocusType.Passive))
                    PopulateSelectDependencyColumnsMenu();

                if (EditorGUILayout.DropdownButton(EditorGUIUtility.TrTempContent(m_CurrentState.name), FocusType.Passive))
                    PopulateStateSources();

                if (EditorGUILayout.DropdownButton(EditorGUIUtility.TrTempContent("Search"), FocusType.Passive))
                    PopulateOpenStateInSearchMenu();
                EditorGUI.BeginChangeCheck();

                EditorGUI.BeginDisabledGroup(!m_CurrentState?.trackSelection ?? true);
                m_LockSelection = GUILayout.Toggle(m_LockSelection, GUIContent.none, Styles.lockButton);
                if (EditorGUI.EndChangeCheck() && !m_LockSelection)
                    OnSelectionChanged();
                EditorGUI.EndDisabledGroup();
            }
        }

        void DrawDependencyTableViews(Event evt)
        {
#if USE_SEARCH_MODULE
            using (SearchMonitor.GetView())
#endif
            {
                if (m_Views != null && m_Views.Count >= 1)
                {
                    EditorGUILayout.BeginHorizontal(GUIStyle.none);
                    var multiView = m_Views.Count == 2;
                    var treeViewRect = multiView ?
                        EditorGUILayout.GetControlRect(false, -1, GUIStyle.none, GUILayout.ExpandHeight(true), GUILayout.Width(Mathf.Ceil(m_Splitter.width - 1))) :
                        EditorGUILayout.GetControlRect(false, -1, GUIStyle.none, GUILayout.ExpandHeight(true));
                    m_Views[0].OnGUI(treeViewRect);
                    if (multiView)
                    {
                        m_Splitter.Draw(evt, treeViewRect);
                        treeViewRect = EditorGUILayout.GetControlRect(false, -1, GUIStyle.none, GUILayout.ExpandHeight(true), GUILayout.ExpandWidth(true));
                        if (m_Views.Count < 2 || m_Views[0] == null)
                            Debug.Log("is null");

                        m_Views[1].OnGUI(treeViewRect);

                        if (evt.type == EventType.Repaint)
                        {
                            GUI.DrawTexture(new Rect(treeViewRect.xMin, treeViewRect.yMin, 1, treeViewRect.height),
                                                EditorGUIUtility.whiteTexture, ScaleMode.StretchToFill, false, 0f, new Color(35 / 255f, 35 / 255f, 35 / 255f), 1f, 0f);
                        }
                    }

                    EditorGUILayout.EndHorizontal();
                }
            }
        }
        #endregion

        static string GetName(in string n)
        {
            var p = n.LastIndexOf('/');
            if (p == -1)
                return n;
            return n.Substring(p+1);
        }

        void PopulateSelectDependencyColumnsMenu()
        {
            var menu = new GenericMenu();
            PopulateSelectDependencyColumnsMenu(menu);
            menu.ShowAsContext();
        }

        void PopulateSelectDependencyColumnsMenu(GenericMenu menu, in string prefix = "")
        {
            var columnSetup = DependencyState.defaultColumns;
            menu.AddItem(new GUIContent($"{prefix}Ref. Count"), (columnSetup & DependencyState.Columns.UsedByRefCount) != 0, () => ToggleColumn(DependencyState.Columns.UsedByRefCount));
            menu.AddItem(new GUIContent($"{prefix}Path"), (columnSetup & DependencyState.Columns.Path) != 0, () => ToggleColumn(DependencyState.Columns.Path));
            menu.AddItem(new GUIContent($"{prefix}Type"), (columnSetup & DependencyState.Columns.Type) != 0, () => ToggleColumn(DependencyState.Columns.Type));
            menu.AddItem(new GUIContent($"{prefix}File Size"), (columnSetup & DependencyState.Columns.Size) != 0, () => ToggleColumn(DependencyState.Columns.Size));
            menu.AddItem(new GUIContent($"{prefix}Runtime Size"), (columnSetup & DependencyState.Columns.RuntimeSize) != 0, () => ToggleColumn(DependencyState.Columns.RuntimeSize));
            menu.AddItem(new GUIContent($"{prefix}Depth"), (columnSetup & DependencyState.Columns.Depth) != 0, () => ToggleColumn(DependencyState.Columns.Depth));
        }

        void PopulateOpenStateInSearchMenu()
        {
            var menu = new GenericMenu();
            foreach(var v in m_Views)
            {
                menu.AddItem(new GUIContent(v.state.name), false, () => OpenStateInSearch(v.state));
            }

            menu.ShowAsContext();
        }

        internal static void OpenStateInSearch(DependencyState state)
        {
            Dependency.ShowSearchWindowWithDependencyQuery(state.context, state.name);
        }

        void ToggleColumn(in DependencyState.Columns dc)
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

        void SetNewDependencyStateSource(DependencyViewerState state)
        {
            PushViewerState(state, createTableViews: true);
        }

        public void PushViewerState(IEnumerable<string> idsOfInterest)
        {
            if (m_CurrentState != null)
                PushViewerState(m_CurrentState.provider.CreateState(GetConfig(), idsOfInterest));
        }

        void PushViewerState(DependencyViewerState state, bool createTableViews = false)
        {
            if (state == null)
                return;

            SetViewerState(state, createTableViews);
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

        void BuildViews(DependencyViewerState state)
        {
            m_Views = state.states.Select(s => CreateTableView(s)).ToList();
            AddViewsToContainer();
        }

        BaseDependencyTableView  CreateTableView(DependencyState state)
        {
#if UNITY_2023_1_OR_NEWER
            return new DependencyTableViewUITk(state, this);
#else
            return new DependencyTableViewIMGUI(state, this);
#endif
        }

        void AddViewsToContainer()
        {
#if UNITY_2023_1_OR_NEWER
            if (m_TableViewContainer != null)
            {
                var splitterPos = EditorPrefs.GetFloat(k_SplitterViewPrefKey, 300f);
                if (m_TwoPaneSplitter != null && m_TwoPaneSplitter.fixedPane != null)
                {
                    splitterPos = m_TwoPaneSplitter.fixedPane.worldBound.width;
                    EditorPrefs.SetFloat(k_SplitterViewPrefKey, splitterPos);
                }

                m_TableViewContainer.Clear();
                m_TwoPaneSplitter = null;
                if (m_Views.Count == 1)
                {
                    m_TableViewContainer.Add(m_Views[0].tableView);
                }
                else if (m_Views.Count == 2)
                {
                    m_TwoPaneSplitter = new TwoPaneSplitView(0, splitterPos, TwoPaneSplitViewOrientation.Horizontal) { name = "DepViewer2Pane" };
                    m_TwoPaneSplitter.viewDataKey = k_SplitterViewDataKey;
                    m_TwoPaneSplitter.Add(m_Views[0].tableView);
                    m_TwoPaneSplitter.Add(m_Views[1].tableView);
                    m_TableViewContainer.Add(m_TwoPaneSplitter);
                }
            }
#endif
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
            IEnumerable<string> idsOfInterest = new string[0];
            if (m_CurrentState.trackSelection)
            {
                idsOfInterest = Dependency.EnumerateIdFromObjects(Selection.objects);
            }
            PushViewerState(m_CurrentState.provider.CreateState(GetConfig(), idsOfInterest));
            Repaint();
        }

        void RefreshState()
        {
            var newState = m_CurrentState.provider.CreateState(GetConfig(), m_CurrentState.globalIds);
            m_CurrentState.config = newState.config;
            m_CurrentState.states = newState.states;
            SetNewDependencyStateSource(m_CurrentState);
            Repaint();
        }

        void SetViewerState(DependencyViewerState state, bool createTableViews)
        {
            createTableViews = createTableViews ||
                    m_CurrentState == null ||
                    m_Views == null ||
                    state.states.Count != m_Views.Count ||
                    m_CurrentState.name != state.name;

            if (m_CurrentState != null)
            {
                foreach(var s in m_CurrentState.states)
                {
                    s.Dispose();
                }
            }

            m_CurrentState = state;

#if UNITY_2022_2_OR_NEWER
            m_DependencyDepthLevel = state.config.depthLevel;
#endif
            m_ShowSceneRefs = state.config.flags.HasFlag(DependencyViewerFlags.ShowSceneRefs);

            if (createTableViews)
            {
                BuildViews(m_CurrentState);
            }
            else
            {
                for (var i = 0; i < m_CurrentState.states.Count; ++i)
                {
                    var view = m_Views[i];
                    var s = m_CurrentState.states[i];
                    view.SetState(s);
                }
            }
            titleContent = m_CurrentState.windowTitle;
        }

        DependencyViewerFlags GetViewerFlags()
        {
            if (m_ShowSceneRefs)
                return DependencyViewerFlags.ShowSceneRefs;
            return DependencyViewerFlags.None;
        }

        DependencyViewerConfig GetConfig()
        {
            return new DependencyViewerConfig(GetViewerFlags()
#if UNITY_2022_2_OR_NEWER
                , m_DependencyDepthLevel
#endif
            );
        }

        void PopulateStateSources()
        {
            var menu = new GenericMenu();
            foreach (var stateProvider in DependencyViewerProviderAttribute.providers.Where(s => s.flags.HasFlag(DependencyViewerFlags.TrackSelection)))
                menu.AddItem(new GUIContent(stateProvider.name), false, () => SetNewDependencyStateSource(stateProvider.CreateState(GetConfig(), Dependency.EnumerateIdFromObjects(Selection.objects))));
            menu.AddSeparator("");
            foreach (var stateProvider in DependencyViewerProviderAttribute.providers.Where(s => !s.flags.HasFlag(DependencyViewerFlags.TrackSelection)))
                menu.AddItem(new GUIContent(stateProvider.name), false, () => SetNewDependencyStateSource(stateProvider.CreateState(GetConfig())));

            menu.ShowAsContext();
        }

        void GotoNextStates()
        {
            SetViewerState(m_History[++m_HistoryCursor], true);
            Repaint();
        }

        void GotoPrevStates()
        {
            SetViewerState(m_History[--m_HistoryCursor], true);
            Repaint();
        }

        DependencyViewerConfig IDependencyViewHost.GetConfig()
        {
            return GetConfig();
        }

        void IDependencyViewHost.Repaint()
        {
            Repaint();
        }

        void IDependencyViewHost.PushViewerState(DependencyViewerState state)
        {
            PushViewerState(state);
        }

        void IDependencyViewHost.ToggleColumn(in DependencyState.Columns dc)
        {
            ToggleColumn(dc);
        }

        void IDependencyViewHost.SelectDependencyColumns(GenericMenu menu, in string prefix)
        {
            PopulateSelectDependencyColumnsMenu(menu, prefix);
        }
    }
}
