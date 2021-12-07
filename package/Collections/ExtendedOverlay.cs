#if UNITY_2021_2_OR_NEWER
using UnityEngine;
using UnityEditor.Overlays;
using UnityEngine.UIElements;
using System.Linq;
using System.Reflection;

namespace UnityEditor.Search.Collections
{
    enum ResizingWindow
    {
        None,
        Left,
        Right,
        Bottom,
        Gripper
    }

    abstract class ExtendedOverlay : Overlay
    {
        static int RESIZER_CONTROL_ID = "OverlayResizer".GetHashCode();

        IMGUIContainer m_CollectionContainer;
        ResizingWindow m_Resizing = ResizingWindow.None;
        
        public ExtendedOverlay()
        {
            #if !USE_SEARCH_EXTENSION_API
            layout = Layout.Panel;
            #endif
        }

        public override VisualElement CreatePanelContent()
        {
            rootElement.pickingMode = PickingMode.Position;
            m_CollectionContainer = new IMGUIContainer(OnGUI);
            m_CollectionContainer.style.width = EditorPrefs.GetFloat("SCO_Width", 250f);
            m_CollectionContainer.style.height = EditorPrefs.GetFloat("SCO_Height", 350f);
            return m_CollectionContainer;
        }

        protected abstract void Render(Event evt);

        private void OnGUI()
        {
            if (!displayed)
                return;

            var evt = Event.current;
            HandleOverlayResize(evt);

            Render(evt);
        }

        private static MethodInfo s_Unclip;
        public static Rect Unclip(in Rect r)
        {
            if (s_Unclip == null)
            {
                var assembly = typeof(GUIUtility).Assembly;
                var type = assembly.GetTypes().First(t => t.Name == "GUIClip");
                s_Unclip = type.GetMethod("Unclip_Rect", BindingFlags.NonPublic | BindingFlags.Static, null, new[] { typeof(Rect) }, null);
            }
            return (Rect)s_Unclip.Invoke(null, new object[] { r });
        }

        internal VisualElement rootElement
        {
            get
            {
                return (VisualElement)typeof(Overlay).GetProperty("rootVisualElement", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(this);
            }
        }

        private void HandleOverlayResize(Event evt)
        {
            if (evt.type == EventType.MouseUp && m_Resizing != ResizingWindow.None)
            {
                GUIUtility.hotControl = 0;
                m_Resizing = ResizingWindow.None;
            }
            else if (evt.type == EventType.MouseDrag && GUIUtility.hotControl == RESIZER_CONTROL_ID)
            {
                switch (m_Resizing)
                {
                    case ResizingWindow.Left:
                        var mousePositionUnclipped = Unclip(new Rect(evt.mousePosition, Vector2.zero)).position;
                        var diff = rootElement.style.left.value.value - mousePositionUnclipped.x;
                        rootElement.style.left = mousePositionUnclipped.x;
                        m_CollectionContainer.style.width = m_CollectionContainer.style.width.value.value + diff;
                        break;
                    case ResizingWindow.Right:
                        m_CollectionContainer.style.width = evt.mousePosition.x;
                        break;
                    case ResizingWindow.Bottom:
                        m_CollectionContainer.style.height = evt.mousePosition.y;
                        break;
                    case ResizingWindow.Gripper:
                        m_CollectionContainer.style.width = evt.mousePosition.x;
                        m_CollectionContainer.style.height = evt.mousePosition.y;
                        break;
                    default:
                        return;
                }

                EditorPrefs.SetFloat("SCO_Width", m_CollectionContainer.style.width.value.value);
                EditorPrefs.SetFloat("SCO_Height", m_CollectionContainer.style.height.value.value);
                evt.Use();
            }
            else
            {
                const float resizeGripperSize = 3;
                var width = m_CollectionContainer.style.width.value.value;
                var height = m_CollectionContainer.style.height.value.value;

                var leftResizeRect = new Rect(0, 0, resizeGripperSize, height);
                var rightResizeRect = new Rect(width - resizeGripperSize, 0, resizeGripperSize, height - resizeGripperSize * 2);
                var bottomResizeRect = new Rect(0, height - resizeGripperSize, width - resizeGripperSize * 2, resizeGripperSize);
                var bottomRightResizeRect = new Rect(width - resizeGripperSize * 2, height - resizeGripperSize * 2, resizeGripperSize * 2, resizeGripperSize * 2);

                EditorGUIUtility.AddCursorRect(leftResizeRect, MouseCursor.ResizeHorizontal);
                EditorGUIUtility.AddCursorRect(rightResizeRect, MouseCursor.ResizeHorizontal);
                EditorGUIUtility.AddCursorRect(bottomResizeRect, MouseCursor.ResizeVertical);
                EditorGUIUtility.AddCursorRect(bottomRightResizeRect, MouseCursor.ResizeUpLeft);
                
                if (evt.type == EventType.MouseDown)
                {
                    if (bottomRightResizeRect.Contains(evt.mousePosition))
                        m_Resizing = ResizingWindow.Gripper;
                    else if (leftResizeRect.Contains(evt.mousePosition))
                        m_Resizing = ResizingWindow.Left;
                    else if (rightResizeRect.Contains(evt.mousePosition))
                        m_Resizing = ResizingWindow.Right;
                    else if (bottomResizeRect.Contains(evt.mousePosition))
                        m_Resizing = ResizingWindow.Bottom;
                    else
                        return;

                    if (m_Resizing != ResizingWindow.None && evt.isMouse)
                    {
                        GUIUtility.hotControl = RESIZER_CONTROL_ID;
                        evt.Use();
                    }
                }
            }
        }

#if UNITY_2022_1_OR_NEWER
        public new void Close()
#else
        public  void Close()
#endif
        {
            displayed = false;
#if UNITY_2022_1_OR_NEWER
            base.Close();
#endif
        }

        public void Repaint()
        {
            containerWindow?.Repaint();
        }
    }
}
#endif
