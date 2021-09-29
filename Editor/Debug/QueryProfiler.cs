#if UNITY_2022_1_OR_NEWER
using System;
using System.Collections;
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

        public QueryExecutionMetrics()
        {
            startTime = DateTime.UtcNow.Ticks;
        }
    }

    class QueryProfiler : EditorWindow
    {
        [SerializeField] private string m_SearchText;
        [SerializeField] private QueryExecutionMetrics m_Stats;

        internal void OnEnable()
        {
            titleContent = new GUIContent("Query Profiler", Icons.quicksearch);
            m_Stats = m_Stats ?? new QueryExecutionMetrics();
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
                    if (GUILayout.Button("Execute", EditorStyles.miniButton))
                        ExecuteQuery(m_SearchText, null);
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
                }
                EditorGUILayout.EndHorizontal();

                GUILayout.Label($"Batch Count: {m_Stats.batchCount}");
                GUILayout.Label($"Result Count: {m_Stats.itemCount}");
                GUILayout.Label($"Execution Time (ms): {m_Stats.executionTimeMs}");
            }
            EditorGUILayout.EndVertical();
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
            m_Stats = new QueryExecutionMetrics();
            var trackerHandle = Profiling.EditorPerformanceTracker.StartTracker("Search");
            var searchContext = SearchService.CreateContext(query);

            void ProcessBatchItems(SearchContext context, IEnumerable<SearchItem> items)
            {
                m_Stats.batchCount++;
                m_Stats.itemCount += items.Count();
            }

            void OnSearchCompleted(SearchContext context)
            {
                m_Stats.executionTimeMs = (long)TimeSpan.FromTicks(DateTime.UtcNow.Ticks - m_Stats.startTime).TotalMilliseconds;
                searchContext.Dispose();
                searchContext = null;
                Profiling.EditorPerformanceTracker.StopTracker(trackerHandle);
                finished?.Invoke();
            }
            
            SearchService.Request(searchContext, ProcessBatchItems, OnSearchCompleted);
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