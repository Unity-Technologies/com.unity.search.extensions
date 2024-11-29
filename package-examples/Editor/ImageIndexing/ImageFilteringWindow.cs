#if NEED_TO_IMPORT_ReflectionUtils
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityEditor.Search
{
    delegate IEditorImageFilter EditorFilterCreatorHandler();

    struct EditorFilterCreator
    {
        public string name;
        public EditorFilterCreatorHandler handler;

        public EditorFilterCreator(string name, EditorFilterCreatorHandler handler)
        {
            this.name = name;
            this.handler = handler;
        }
    }

    class ImageFilteringWindow : EditorWindow
    {
        Texture2D m_RefTexture;
        IEditorImageFilter m_CurrentFilter;
        Dictionary<string, IEditorImageFilter> m_AllFilters;

        [MenuItem("Window/Search/Image Indexing/Image Filtering")]
        static void Init()
        {
            // Get existing open window or if none, make a new one:
            var window = (ImageFilteringWindow)EditorWindow.GetWindow(typeof(ImageFilteringWindow));
            window.Show();
        }

        void OnEnable()
        {
            GetAllFilters();
            m_CurrentFilter = m_AllFilters.Values.FirstOrDefault();

            var originalTextureArea = new VisualElement();
            originalTextureArea.style.flexGrow = 1;
            originalTextureArea.style.flexShrink = 0;
            originalTextureArea.style.unityBackgroundScaleMode = ScaleMode.ScaleToFit;
            UpdateTextureArea(originalTextureArea, null);

            var textureArea = new VisualElement();
            textureArea.style.flexGrow = 1;
            textureArea.style.flexShrink = 0;
            textureArea.style.unityBackgroundScaleMode = ScaleMode.ScaleToFit;
            UpdateTextureArea(textureArea, null);

            var filterParametersArea = new VisualElement();
            filterParametersArea.style.flexDirection = FlexDirection.Column;

            var dropDownField = new DropdownField("Filter", m_AllFilters.Keys.ToList(), 0);
            dropDownField.RegisterValueChangedCallback(evt =>
            {
                if (!m_AllFilters.TryGetValue(evt.newValue, out var filter))
                    return;
                m_CurrentFilter = filter;
                SetupFilterParametersArea(m_CurrentFilter, filterParametersArea, () => ApplyTextureFiltering(textureArea));
                ApplyTextureFiltering(textureArea);
            });
            rootVisualElement.Add(dropDownField);

            var textureField = new UnityEditor.UIElements.ObjectField("Texture") { objectType = typeof(Texture2D) };
            textureField.RegisterValueChangedCallback(evt =>
            {
                m_RefTexture = evt.newValue as Texture2D;
                UpdateTextureArea(originalTextureArea, m_RefTexture);
                ApplyTextureFiltering(textureArea);
            });
            rootVisualElement.Add(textureField);

            rootVisualElement.Add(filterParametersArea);
            rootVisualElement.Add(originalTextureArea);
            rootVisualElement.Add(textureArea);

            SetupFilterParametersArea(m_CurrentFilter, filterParametersArea, () => ApplyTextureFiltering(textureArea));
        }

        void ApplyTextureFiltering(VisualElement textureArea)
        {
            var t = FilterTexture();
            UpdateTextureArea(textureArea, t);
        }

        Texture2D FilterTexture()
        {
            if (m_RefTexture == null || m_CurrentFilter == null)
                return null;

            var imageInfo = new ImagePixels(m_RefTexture);
            var filteredImage = m_CurrentFilter.Apply(imageInfo);

            return TextureUtils.ToTexture(filteredImage.pixels, filteredImage.width, filteredImage.height);
        }

        static void UpdateTextureArea(VisualElement textureArea, Texture2D texture)
        {
            textureArea.Clear();
            if (texture != null)
            {
                textureArea.style.backgroundImage = new StyleBackground(texture);
            }
            else
            {
                textureArea.style.backgroundImage = null;
                textureArea.style.backgroundColor = new StyleColor(new Color(42.0f / 255.0f, 42.0f / 255.0f, 42.0f / 255.0f));
            }
        }

        static void SetupFilterParametersArea(IEditorImageFilter filter, VisualElement filterParameterArea, Action onFilterChanged)
        {
            filterParameterArea.Clear();
            if (filter == null)
                return;

            filter.PopulateParameters(filterParameterArea, onFilterChanged);
        }

        void GetAllFilters()
        {
            var allCreators = ReflectionUtils.LoadAllMethodsWithAttribute<EditorFilterAttribute, EditorFilterCreator>((mi, attribute, handler) =>
            {
                if (handler is EditorFilterCreatorHandler efch)
                    return new EditorFilterCreator(attribute.name, efch);
                throw new ArgumentException($"Handler should be of type {typeof(EditorFilterCreatorHandler)}.", nameof(handler));
            }, MethodSignature.FromDelegate<EditorFilterCreatorHandler>());

            m_AllFilters = allCreators.ToDictionary(creator => creator.name, creator => creator.handler());
        }
    }
}
#endif
