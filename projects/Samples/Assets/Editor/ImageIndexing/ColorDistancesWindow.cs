using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityEditor.Search
{
    class ColorDistancesWindow : EditorWindow
    {
        Color m_RefColor = Color.white;
        Color m_TestColor = Color.black;

        [MenuItem("Window/Search/Image Indexing/Color Distances")]
        static void Init()
        {
            // Get existing open window or if none, make a new one:
            var window = (ColorDistancesWindow)EditorWindow.GetWindow(typeof(ColorDistancesWindow));
            window.Show();
        }

        void OnEnable()
        {
            var refColorField = new ColorField("Reference");
            refColorField.RegisterValueChangedCallback((evt =>
            {
                m_RefColor = evt.newValue;
                UpdateDistancesDisplay();
            }));
            refColorField.value = m_RefColor;
            rootVisualElement.Add(refColorField);

            var testColorField = new ColorField("Test");
            testColorField.RegisterValueChangedCallback(evt =>
            {
                m_TestColor = evt.newValue;
                UpdateDistancesDisplay();
            });
            testColorField.value = m_TestColor;
            rootVisualElement.Add(testColorField);

            AddDistanceFloatField("rgb", "RGB Distance", rootVisualElement);
            AddDistanceFloatField("cie", "CIE Distance", rootVisualElement);
            AddDistanceFloatField("yuv", "YUV Distance", rootVisualElement);
            AddDistanceFloatField("similarity", "Weighted Similarity", rootVisualElement);

            UpdateDistancesDisplay();
        }

        void UpdateDistancesDisplay()
        {
            var rgbValueField = rootVisualElement.Q<FloatField>("rgb");
            var cieValueField = rootVisualElement.Q<FloatField>("cie");
            var yuvValueField = rootVisualElement.Q<FloatField>("yuv");
            var simValueField = rootVisualElement.Q<FloatField>("similarity");

            var rgbDistance = ImageUtils.ColorDistance(m_TestColor, m_RefColor);
            var cieDistance = ImageUtils.CIELabDistance(m_TestColor, m_RefColor);
            var yuvDistance = ImageUtils.YUVDistance(m_TestColor, m_RefColor);
            var similarity = ImageUtils.WeightedSimilarity(m_TestColor, 1.0, m_RefColor);

            rgbValueField.SetValueWithoutNotify(rgbDistance);
            cieValueField.SetValueWithoutNotify(cieDistance);
            yuvValueField.SetValueWithoutNotify(yuvDistance);
            simValueField.SetValueWithoutNotify((float)similarity);
        }

        static void AddDistanceFloatField(string name, string label, VisualElement root)
        {
            var field = new FloatField(label) { name = name };
            field.SetEnabled(false);
            root.Add(field);
        }
    }
}
