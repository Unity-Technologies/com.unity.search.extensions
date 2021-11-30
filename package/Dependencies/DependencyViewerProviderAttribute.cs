#if USE_SEARCH_TABLE
using System;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

namespace UnityEditor.Search
{
    [AttributeUsage(AttributeTargets.Method)]
    class DependencyViewerProviderAttribute : Attribute
    {
        static List<DependencyViewerProviderAttribute> s_StateProviders;

        private Func<DependencyViewerFlags, DependencyViewerState> handler;
        public int id { get; private set; }
        public string name { get; private set; }
        public DependencyViewerFlags flags { get; private set; }

        public static IEnumerable<DependencyViewerProviderAttribute> providers
        {
            get
            {
                if (s_StateProviders == null)
                    FetchStateProviders();
                return s_StateProviders;
            }
        }

        public DependencyViewerProviderAttribute(DependencyViewerFlags flags = DependencyViewerFlags.None, string name = null)
        {
            this.flags = flags;
            this.name = name;
        }

        static void FetchStateProviders()
        {
            s_StateProviders = new List<DependencyViewerProviderAttribute>();
            var methods = TypeCache.GetMethodsWithAttribute<DependencyViewerProviderAttribute>();
            foreach (var mi in methods)
            {
                try
                {
                    var attr = mi.GetCustomAttributes(typeof(DependencyViewerProviderAttribute), false).Cast<DependencyViewerProviderAttribute>().First();
                    attr.handler = Delegate.CreateDelegate(typeof(Func<DependencyViewerFlags, DependencyViewerState>), mi) as Func<DependencyViewerFlags, DependencyViewerState>;
                    attr.name = attr.name ?? ObjectNames.NicifyVariableName(mi.Name);
                    s_StateProviders.Add(attr);
                    attr.id = s_StateProviders.Count - 1;
                }
                catch (Exception e)
                {
                    Debug.LogError($"Cannot register State provider: {mi.Name}\n{e}");
                }
            }
        }

        public static DependencyViewerProviderAttribute GetProvider(int id)
        {
            if (id < 0 || id >= providers.Count())
                return null;
            return s_StateProviders[id];
        }

        public static DependencyViewerProviderAttribute GetDefault()
        {
            var d = providers.FirstOrDefault(p => p.flags.HasFlag(DependencyViewerFlags.TrackSelection));
            if (d != null)
                return d;
            return providers.First();
        }

        public DependencyViewerState CreateState(DependencyViewerFlags flags)
        {
            var state = handler(flags);
            if (state == null)
                return null;
            state.flags |= flags;
            state.viewerProviderId = id;
            return state;
        }
    }
}
#endif
