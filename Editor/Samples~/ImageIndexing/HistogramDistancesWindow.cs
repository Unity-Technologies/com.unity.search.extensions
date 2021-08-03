using System;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityEditor.Search
{
    class HistogramDistancesWindow : EditorWindow
    {
        Texture2D m_RefTexture;
        Texture2D m_TestTexture;

        [MenuItem("Window/Search/Image Indexing/Histogram Distances")]
        static void Init()
        {
            // Get existing open window or if none, make a new one:
            var window = (HistogramDistancesWindow)EditorWindow.GetWindow(typeof(HistogramDistancesWindow));
            window.Show();
        }

        void OnEnable()
        {
            var refTextureField = new ObjectField("Reference");
            refTextureField.objectType = typeof(Texture2D);
            refTextureField.RegisterValueChangedCallback((evt =>
            {
                m_RefTexture = evt.newValue as Texture2D;
                UpdateDistancesDisplay();
            }));
            refTextureField.value = m_RefTexture;
            rootVisualElement.Add(refTextureField);

            var testTextureField = new ObjectField("Test");
            testTextureField.objectType = typeof(Texture2D);
            testTextureField.RegisterValueChangedCallback(evt =>
            {
                m_TestTexture = evt.newValue as Texture2D;
                UpdateDistancesDisplay();
            });
            testTextureField.value = m_TestTexture;
            rootVisualElement.Add(testTextureField);

            foreach (int distanceModel in Enum.GetValues(typeof(HistogramDistance)))
            {
                AddDistanceFloatField((HistogramDistance)distanceModel, rootVisualElement);
            }

            UpdateDistancesDisplay();
        }

        void UpdateDistancesDisplay()
        {
            if (m_RefTexture == null || m_TestTexture == null)
                return;
            Histogram refHist = new Histogram();
            Histogram testHist = new Histogram();
            ImageUtils.ComputeHistogram(m_RefTexture, refHist);
            ImageUtils.ComputeHistogram(m_TestTexture, testHist);

            foreach (int distanceModelValue in Enum.GetValues(typeof(HistogramDistance)))
            {
                var distanceModel = (HistogramDistance)distanceModelValue;
                var floatField = rootVisualElement.Q<FloatField>(distanceModel.ToString());

                var distance = ImageUtils.HistogramDistance(testHist, refHist, distanceModel);
                floatField.SetValueWithoutNotify(distance);
            }
        }

        static void AddDistanceFloatField(HistogramDistance distanceModel, VisualElement root)
        {
            var name = distanceModel.ToString();
            var field = new FloatField(name) { name = name };
            field.SetEnabled(false);
            root.Add(field);
        }
    }
}
