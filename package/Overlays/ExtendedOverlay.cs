#if UNITY_2021_2_OR_NEWER
using UnityEngine;
using UnityEditor.Overlays;
using UnityEngine.UIElements;
using System.Reflection;
using System.Linq;

namespace UnityEditor.Search
{
    enum ResizingWindow
    {
        None,
        Left,
        Right,
        Bottom,
        Gripper
    }

    public abstract class ExtendedOverlay : Overlay
    {
        const float resizeGripperSize = 3;
        static int RESIZER_CONTROL_ID = "OverlayResizer".GetHashCode();

        private VisualElement m_ContainerElement;
        private ResizingWindow m_Resizing = ResizingWindow.None;
        
        public ExtendedOverlay()
        {
            #if !USE_SEARCH_EXTENSION_API
            layout = Layout.Panel;
            #endif
        }

        public override VisualElement CreatePanelContent()
        {
            rootElement.pickingMode = PickingMode.Position;
            m_ContainerElement = new IMGUIContainer(OnGUI);
            var hostedElement = CreateContainerContent();
            if (hostedElement != null)
            {
                hostedElement.style.flexGrow = 1f;
                m_ContainerElement.style.paddingLeft = resizeGripperSize;
                m_ContainerElement.style.paddingRight = resizeGripperSize;
                m_ContainerElement.style.paddingBottom = resizeGripperSize;
                m_ContainerElement.Add(hostedElement);
            }

            var defaultSize = GetDefaultSize();
            var bgcolor = m_ContainerElement.style.backgroundColor.value;
            m_ContainerElement.style.backgroundColor = new Color(bgcolor.r, bgcolor.g, bgcolor.b, 0.1f);
            m_ContainerElement.style.flexGrow = 1f;
            m_ContainerElement.style.width = EditorPrefs.GetFloat(widthKey, defaultSize.x);
            m_ContainerElement.style.height = EditorPrefs.GetFloat(heightKey, defaultSize.y);
            return m_ContainerElement;
        }

        protected virtual void Render(Event evt) {}
        protected virtual VisualElement CreateContainerContent() => null;
        protected virtual Vector2 GetDefaultSize() => new Vector2(250f, 350f);
        protected virtual string widthKey => $"{GetType().Name}_Width";
        protected virtual string heightKey => $"{GetType().Name}_Height";

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
                        m_ContainerElement.style.width = m_ContainerElement.style.width.value.value + diff;
                        break;
                    case ResizingWindow.Right:
                        m_ContainerElement.style.width = evt.mousePosition.x;
                        break;
                    case ResizingWindow.Bottom:
                        m_ContainerElement.style.height = evt.mousePosition.y;
                        break;
                    case ResizingWindow.Gripper:
                        m_ContainerElement.style.width = evt.mousePosition.x;
                        m_ContainerElement.style.height = evt.mousePosition.y;
                        break;
                    default:
                        return;
                }

                EditorPrefs.SetFloat(widthKey, m_ContainerElement.style.width.value.value);
                EditorPrefs.SetFloat(heightKey, m_ContainerElement.style.height.value.value);
                evt.Use();
            }
            else
            {
                var width = m_ContainerElement.style.width.value.value;
                var height = m_ContainerElement.style.height.value.value;

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
