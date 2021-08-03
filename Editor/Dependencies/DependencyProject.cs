using UnityEngine;

namespace UnityEditor.Search
{
	[InitializeOnLoad]
	static class DependencyProject
	{
		static GUIStyle miniLabelAlignRight = null;

		static DependencyProject()
		{
			EditorApplication.projectWindowItemOnGUI -= DrawDependencies;
			EditorApplication.projectWindowItemOnGUI += DrawDependencies;
		}

		static void DrawDependencies(string guid, Rect rect)
		{
			// Do not render anything if not in details view.
			if (rect.height > 25f || Event.current.type != EventType.Repaint)
				return;

			var count = Dependency.GetReferenceCount(guid);
			if (count == -1)
				return;

			if (miniLabelAlignRight == null)
				miniLabelAlignRight = CreateLabelStyle();

			float maxWidth = miniLabelAlignRight.fixedWidth;
			var r = new Rect(rect.xMax - maxWidth, rect.y, maxWidth, rect.height);
			GUI.Label(r, Utils.FormatCount((ulong)count), miniLabelAlignRight);
		}

		static GUIStyle CreateLabelStyle()
		{
			return new GUIStyle(EditorStyles.miniLabel)
			{
				alignment = TextAnchor.MiddleRight,
				padding = new RectOffset(0, 4, 0, 0),
				fixedWidth = 16f
			};
		}
	}
}
