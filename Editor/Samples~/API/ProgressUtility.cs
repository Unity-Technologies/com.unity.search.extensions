#if USE_SEARCH_TABLE
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;

using static UnityEditor.Progress;

public static class ProgressUtility
{
    public static int RunTask(string name, string description, Func<int, object, IEnumerator> taskHandler, Options options = Options.None, int parentId = -1, object userData = null)
    {
        var progressId = Start(name, description, options, parentId);
        s_Tasks.Add(new Task { id = progressId, handler = taskHandler, userData = userData, iterators = new Stack<IEnumerator>() });

        EditorApplication.update -= RunTasks;
        EditorApplication.update += RunTasks;

        return progressId;
    }

    private static void RunTasks()
    {
        for (var taskIndex = s_Tasks.Count - 1; taskIndex >= 0; --taskIndex)
        {
            var task = s_Tasks[taskIndex];
            try
            {
                if (task.iterators.Count == 0)
                    task.iterators.Push(task.handler(task.id, task.userData));

                var iterator = task.iterators.Peek();
                var finished = !iterator.MoveNext();

                if (finished)
                {
                    if (task.iterators.Count > 1)
                    {
                        ++taskIndex;
                        task.iterators.Pop();
                    }
                    else
                    {
                        Finish(task.id, Status.Succeeded);
                        s_Tasks.RemoveAt(taskIndex);
                    }

                    continue;
                }

                var cEnumerable = iterator.Current as IEnumerable;
                if (cEnumerable != null)
                {
                    ++taskIndex;
                    task.iterators.Push(cEnumerable.GetEnumerator());
                    continue;
                }

                var cEnumerator = iterator.Current as IEnumerator;
                if (cEnumerator != null)
                {
                    ++taskIndex;
                    task.iterators.Push(cEnumerator);
                    continue;
                }

                var report = iterator.Current as TaskReport;
                if (report?.error != null)
                {
                    SetDescription(task.id, report.error);
                    Finish(task.id, Status.Failed);
                    s_Tasks.RemoveAt(taskIndex);
                }
                else
                {
                    if (report != null)
                    {
                        if (string.IsNullOrEmpty(report.description))
                            Report(task.id, report.progress);
                        else
                            Report(task.id, report.progress, report.description);
                    }
                    else if ((GetOptions(task.id) & Options.Indefinite) == Options.Indefinite)
                    {
                        Report(task.id, -1f);
                    }
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogException(ex);
                SetDescription(task.id, ex.Message);
                Finish(task.id, Status.Failed);
                s_Tasks.RemoveAt(taskIndex);
            }
        }

        if (s_Tasks.Count == 0)
            EditorApplication.update -= RunTasks;
    }

    public class TaskReport
    {
        public TaskReport(float progress = -1f, string description = null)
        {
            this.progress = progress;
            this.description = description;
            error = null;
        }

        public float progress { get; internal set; }
        public string description { get; internal set; }
        public string error { get; internal set; }
    }

    public class TaskError : TaskReport
    {
        public TaskError(string error)
            : base(0f)
        {
            this.error = error;
        }
    }

    class Task
    {
        public int id { get; internal set; }
        public Func<int, object, IEnumerator> handler { get; internal set; }
        public object userData { get; internal set; }
        public Stack<IEnumerator> iterators { get; internal set; }
    }

    private static readonly List<Task> s_Tasks = new List<Task>();

    public class BaseProgressTest
    {
        static readonly ConcurrentBag<int> k_AddedProgress = new ConcurrentBag<int>();

        [TearDown]
        public void ClearAddedProgresses()
        {
            var progressStillExist = false;
            while (!k_AddedProgress.IsEmpty)
            {
                if (k_AddedProgress.TryTake(out var progressId))
                {
                    if (Progress.Exists(progressId))
                    {
                        progressStillExist = true;
                        Progress.Remove(progressId);
                    }
                }
            }

            Assert.IsFalse(progressStillExist, "Progresses were not removed");
        }

        public static int StartProgress(string name, string description = null, Options options = Options.None, int parentId = -1)
        {
            var progressId = Progress.Start(name, description, options, parentId);
            k_AddedProgress.Add(progressId);
            return progressId;
        }
    }
}
#endif
