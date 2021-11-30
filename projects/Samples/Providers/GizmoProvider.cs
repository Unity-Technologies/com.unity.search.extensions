#if DEPENDS_ON_INTERNAL_APIS
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace UnityEditor.Search.Providers
{
	static class GizmoSearchProvider
	{
		static string id = "gizmo";
		static QueryEngine<AInfo> queryEngine;

		[SearchItemProvider]
		public static SearchProvider CreateProvider()
		{
			queryEngine = new QueryEngine<AInfo>();
			queryEngine.SetSearchDataCallback(YieldGizmoNames, s => s, StringComparison.OrdinalIgnoreCase);

			return new SearchProvider(id, "Gizmo")
			{
				isExplicitProvider = true,
				fetchItems = FetchItems,
				fetchDescription = FetchDescription,
				fetchThumbnail = FetchThumbnail
			};
		}

		[SearchActionsProvider]
		public static IEnumerable<SearchAction> ActionHandlers()
		{
			return new[]
			{
				new SearchAction(id, "toggle_icon", new GUIContent("Toggle icon"), ToggleIcon) { enabled = CanToggleIcon },
				new SearchAction(id, "toggle_gizmos", new GUIContent("Toggle gizmos"), ToggleGizmo) { enabled = CanToggleGizmo }
			};
		}

		static IEnumerable<string> YieldGizmoNames(AInfo arg)
		{
			yield return arg.m_DisplayText;
			if (arg.HasGizmo())
				yield return "hasgizmo";
			if (arg.HasIcon())
				yield return "hasicon";
			if (!arg.IsDisabled())
				yield return "enabled";
		}

		static IEnumerable<SearchItem> FetchItems(SearchContext context, List<SearchItem> items, SearchProvider provider)
		{
			if (string.IsNullOrEmpty(context.searchText))
				yield break;

			var query = queryEngine.Parse(context.searchQuery);
			if (!query.valid)
				yield break;

			var annotations = AnnotationUtility.GetAnnotations()
				.Select(annotation => new AInfo(annotation.gizmoEnabled == 1, annotation.iconEnabled == 1, annotation.flags, annotation.classID, annotation.scriptClass))
				.OrderBy(a => a.m_DisplayText);
			foreach (var a in query.Apply(annotations))
				yield return provider.CreateItem(context, a.m_ClassID + a.m_ScriptClass, a.m_DisplayText, null, null, a);
		}

		static string FetchDescription(SearchItem item, SearchContext context)
		{
			var a = (AInfo)item.data;
			var description = $"{(a.HasIcon() ? a.m_IconEnabled ? "Icon Enabled" : "Icon Disabled" : "No Icon")} " +
				$"{(a.HasGizmo() ? a.m_GizmoEnabled ? "Gizmos Enabled" : "Gizmos Disabled" : "No Gizmos")}";

			if (item.options.HasAny(SearchItemOptions.Compacted))
				return $"{item.label} / {description}";
			return description;
		}

		static Texture2D FetchThumbnail(SearchItem item, SearchContext context) => (item.data as AInfo).Thumb;

		static void ToggleGizmo(SearchItem item)
		{
			var a = (AInfo)item.data;
			a.m_GizmoEnabled = !a.m_GizmoEnabled;
			AnnotationUtility.SetGizmoEnabled(a.m_ClassID, a.m_ScriptClass, a.m_GizmoEnabled ? 1 : 0, true);
			Refresh(item);
		}

		static void ToggleIcon(SearchItem item)
		{
			var a = (AInfo)item.data;
			a.m_IconEnabled = !a.m_IconEnabled;
			AnnotationUtility.SetIconEnabled(a.m_ClassID, a.m_ScriptClass, a.m_IconEnabled ? 1 : 0);
			Refresh(item);
		}

		static void Refresh(SearchItem item)
		{
			SceneView.RepaintAll();
			item.description = null;
		}

		static bool CanToggleIcon(IReadOnlyCollection<SearchItem> arg) => arg.All(e => ((AInfo)e.data).HasIcon());
		static bool CanToggleGizmo(IReadOnlyCollection<SearchItem> arg) => arg.All(e => ((AInfo)e.data).HasGizmo());
	}
}
#endif
