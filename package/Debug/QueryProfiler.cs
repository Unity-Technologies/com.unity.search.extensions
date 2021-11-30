#if UNITY_2022_1_OR_NEWER
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.Profiling;

namespace UnityEditor.Search
{
    [Serializable]
    class QueryExecutionMetrics
    {
        public long startTime; 
        public long executionTimeMs;
        public long batchCount;
        public long itemCount;
        public bool finished;
        public int ticks;
        public long startMem;
        public long deltaMem;
        public int progressId;

        public static QueryExecutionMetrics StartNew()
        {
            var metrics = new QueryExecutionMetrics();
            return metrics.Start();
        }

        ~QueryExecutionMetrics()
        {
            if (progressId != -1)
                Progress.Finish(progressId);
        }

        public QueryExecutionMetrics Start()
        {
            startTime = DateTime.UtcNow.Ticks;
            EditorApplication.tick += Tick;
            progressId = Progress.Start("Running query", null, Progress.Options.Indefinite);
            startMem = Profiler.GetMonoHeapSizeLong();
            return this;
        }

        public void Stop()
        {
            deltaMem = Profiler.GetMonoHeapSizeLong() - startMem;
            Progress.Finish(progressId);
            progressId = -1;
            EditorApplication.tick -= Tick;
            executionTimeMs = elapsedTimeMs;
            finished = true;
        }

        void Tick()
        {
            ticks++;
        }

        public long elapsedTimeMs => (long)TimeSpan.FromTicks(DateTime.UtcNow.Ticks - startTime).TotalMilliseconds;
    }

    class QueryProfiler : EditorWindow
    {
        static class Styles
        {
            public static readonly GUIStyle label = new GUIStyle(EditorStyles.label) { richText = true };
        }

        [SerializeField] private string m_SearchText;
        [SerializeField] private QueryExecutionMetrics m_Stats;
        [SerializeField] private List<string> m_ActiveProviders;
        [SerializeField] private bool m_DebugQuery;

        private SearchContext m_SearchContext;

        internal void OnEnable()
        {
            titleContent = new GUIContent("Query Profiler", Icons.quicksearch);
            m_Stats = m_Stats ?? new QueryExecutionMetrics();
            m_ActiveProviders = m_ActiveProviders ?? SearchService.GetActiveProviders().Select(p => p.id).ToList();
        }

        internal void OnDisable()
        {
        }

        internal void OnGUI()
        {
            EditorGUILayout.BeginVertical();
            {
                EditorGUILayout.BeginHorizontal();
                {
                    m_SearchText = EditorGUILayout.TextField(m_SearchText, GUILayout.ExpandWidth(true));
                    if (EditorGUILayout.DropdownButton(EditorGUIUtility.TrTextContent("Providers"), FocusType.Keyboard))
                        SelectProvidersMenu();
                    if (GUILayout.Button("Execute", EditorStyles.miniButton))
                        ExecuteQuery(m_SearchText, null);
                    m_DebugQuery = GUILayout.Toggle(m_DebugQuery, "Debug");
                    if (GUILayout.Button("Profile", EditorStyles.miniButton))
                        ProfileQuery(m_SearchText, ProfilerDriver.deepProfiling);
                    if (!ProfilerDriver.deepProfiling)
                    {
                        if (GUILayout.Button("Enable Deep Profile", EditorStyles.miniButton))
                            SetProfilerDeepProfile(true);
                    }
                    else
                    {
                        if (GUILayout.Button("Disable Deep Profile", EditorStyles.miniButton))
                            SetProfilerDeepProfile(false);
                    }
                    if (GUILayout.Button("Open", EditorStyles.miniButton))
                        OpenQuery(m_SearchText);
                }
                EditorGUILayout.EndHorizontal();

                GUILayout.Label($"Batch Count: {m_Stats.batchCount}");
                GUILayout.Label($"Result Count: {m_Stats.itemCount}");
                GUILayout.Label($"Application Ticks: {m_Stats.ticks}");
                if (m_Stats.finished)
                    GUILayout.Label($"Memory Delta: {Utils.FormatBytes(m_Stats.deltaMem)}"); 
                GUILayout.Label($"Execution Time (ms): {GetExecutionStatus(m_Stats)}", Styles.label);
            }
            EditorGUILayout.EndVertical();
        }

        string GetExecutionStatus(in QueryExecutionMetrics stats)
        {
            if (stats.startTime == 0)
                return "Not run";
            if (!stats.finished)
                return $"{stats.elapsedTimeMs} (<i>Executing...</i>)";
            return stats.executionTimeMs.ToString();
        }

        private void SelectProvidersMenu()
        {
            var menu = new GenericMenu();
            foreach (var p in SearchService.OrderedProviders)
            {
                menu.AddItem(new GUIContent($"{p.name} ({p.filterId})"), m_ActiveProviders.Contains(p.id), () => ToggleProvider(p.id));
            }
            menu.ShowAsContext();
        }

        private void ToggleProvider(string id)
        {
            if (m_ActiveProviders.Contains(id))
                m_ActiveProviders.Remove(id);
            else
                m_ActiveProviders.Add(id);
        }

        private void OpenQuery(string searchText)
        {
            SearchService.ShowWindow(SearchService.CreateContext(searchText));
        }

        private void ProfileQuery(string query, bool deepProfile)
        {
            StartProfilerRecording("Search", true, deepProfile, () =>
            {
                EditorApplication.delayCall += () =>
                {
                    ExecuteQuery(query, () =>
                    {
                        EditorApplication.delayCall += () => StopProfilerRecordingAndOpenProfiler();
                    });
                };
            });
        }

        private void ExecuteQuery(string query, Action finished)
        {
            if (m_Stats != null && !m_Stats.finished)
                m_Stats.Stop();

            if (m_SearchContext != null)
            {
                m_SearchContext.Dispose();
                m_SearchContext = null;
            }

            m_Stats = QueryExecutionMetrics.StartNew();
            var trackerHandle = Profiling.EditorPerformanceTracker.StartTracker("Search");
            m_SearchContext = SearchService.CreateContext(m_ActiveProviders, query, m_DebugQuery ? SearchFlags.Debug : SearchFlags.None);

            void ProcessBatchItems(SearchContext context, IEnumerable<SearchItem> items)
            {
                m_Stats.batchCount++;
                m_Stats.itemCount += items.Count();
                Repaint();
            }

            void OnSearchCompleted(SearchContext context)
            {
                m_SearchContext.Dispose();
                m_SearchContext = null;
                Profiling.EditorPerformanceTracker.StopTracker(trackerHandle);
                m_Stats.Stop();
                finished?.Invoke();
                Repaint();
            }
            
            SearchService.Request(m_SearchContext, ProcessBatchItems, OnSearchCompleted);
        }

        static bool StartProfilerRecording(string markerFilter, bool editorProfile, bool deepProfile, Action onProfilerReady)
        {
            var editorProfileStr = editorProfile ? "editor" : "playmode";
            var deepProfileStr = deepProfile ? " - deep profile" : "";
            var hasMarkerFilter = !string.IsNullOrEmpty(markerFilter);
            var markerStr = hasMarkerFilter ? $"- MarkerFilter: {markerFilter}" : "";
            Debug.Log($"Start profiler recording: {editorProfileStr} {deepProfileStr} {markerStr}...");

            EnableProfiler(false);

            EditorApplication.delayCall += () =>
            {
                ProfilerDriver.ClearAllFrames();
                ProfilerDriver.profileEditor = editorProfile;
                ProfilerDriver.deepProfiling = deepProfile;
                if (hasMarkerFilter)
                    SetMarkerFiltering(markerFilter);

                EditorApplication.delayCall += () => 
                {
                    EnableProfiler(true);
                    onProfilerReady?.Invoke();
                };
            };

            return true;
        }

        static void StopProfilerRecording(Action toProfilerStopped = null)
        {
            SetMarkerFiltering("");
            EnableProfiler(false);
            Debug.Log($"Stop profiler recording.");

            if (toProfilerStopped != null)
                EditorApplication.delayCall += () => toProfilerStopped();
        }

        static void StopProfilerRecordingAndOpenProfiler()
        {
            StopProfilerRecording(() => OpenProfilerWindow());
        }

        static void EnableProfiler(in bool enable)
        {
            ProfilerDriver.enabled = enable;
            SessionState.SetBool("ProfilerEnabled", enable);
        }

        static EditorWindow OpenProfilerWindow()
        {
            var profilerWindow = EditorWindow.CreateWindow<ProfilerWindow>();
            var cpuProfilerModule = profilerWindow.GetProfilerModule<UnityEditorInternal.Profiling.CPUOrGPUProfilerModule>(ProfilerArea.CPU);
            cpuProfilerModule.ViewType = ProfilerViewType.Hierarchy;
            profilerWindow.Show();
            return profilerWindow;
        }

        static void SetProfilerDeepProfile(in bool deepProfile)
        {
            ProfilerWindow.SetEditorDeepProfiling(deepProfile);
        }

        static void SetMarkerFiltering(in string markerName)
        {
            ProfilerDriver.SetMarkerFiltering(markerName);
        }

        [MenuItem("Window/Search/Query Profiler")]
        internal static void ShowWindow()
        {
            EditorWindow.CreateWindow<QueryProfiler>().Show(true);
        }
    }
}
#endif