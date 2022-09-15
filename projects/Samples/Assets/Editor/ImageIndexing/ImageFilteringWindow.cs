#if USE_SEARCH_MODULE
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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

        void CreateGUI()
        {
            GetAllFilters();
            m_CurrentFilter = m_AllFilters.Values.FirstOrDefault();

            var originalTextureArea = new VisualElement();
            originalTextureArea.style.flexGrow = 1;
            originalTextureArea.style.flexShrink = 0;
            originalTextureArea.style.backgroundPositionX = new BackgroundPosition(BackgroundPositionKeyword.Center);
            originalTextureArea.style.backgroundPositionY = new BackgroundPosition(BackgroundPositionKeyword.Center);
            originalTextureArea.style.backgroundRepeat = new BackgroundRepeat(Repeat.NoRepeat, Repeat.NoRepeat);
            originalTextureArea.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Contain);
            UpdateTextureArea(originalTextureArea, null);

            var textureArea = new VisualElement();
            textureArea.style.flexGrow = 1;
            textureArea.style.flexShrink = 0;
            textureArea.style.backgroundPositionX = new BackgroundPosition(BackgroundPositionKeyword.Center);
            textureArea.style.backgroundPositionY = new BackgroundPosition(BackgroundPositionKeyword.Center);
            textureArea.style.backgroundRepeat = new BackgroundRepeat(Repeat.NoRepeat, Repeat.NoRepeat);
            textureArea.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Contain);
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

        static Type s_MethodSignatureType = null;
        static MethodInfo s_MethodSignatureFromDelegateMethodInfo = null;
        static MethodInfo s_LoadAllMethodsWithAttributeMethodInfo = null;
        void GetAllFilters()
        {
            if (s_MethodSignatureType == null || s_MethodSignatureFromDelegateMethodInfo == null)
            {
                var assembly = typeof(SearchService).Assembly;
                s_MethodSignatureType = assembly.GetType("UnityEditor.Search.MethodSignature");
                if (s_MethodSignatureType == null)
                    throw new Exception("Could not find MethodSignature type.");

                var mi = s_MethodSignatureType.GetMethod("FromDelegate", BindingFlags.Public | BindingFlags.Static);
                if (mi == null)
                    throw new Exception("Could not find FromDelegate method.");
                s_MethodSignatureFromDelegateMethodInfo = mi.MakeGenericMethod(new[] { typeof(EditorFilterCreatorHandler) });
                if (s_MethodSignatureFromDelegateMethodInfo == null)
                    throw new Exception("Could not make generic FromDelegate method.");
            }
            if (s_LoadAllMethodsWithAttributeMethodInfo == null)
            {
                var assembly = typeof(SearchService).Assembly;
                var reflectionUtilsType = assembly.GetType("UnityEditor.Search.ReflectionUtils");
                if (reflectionUtilsType == null)
                    throw new Exception("Could not find ReflectionUtils type.");

                MethodInfo mi = null;
                var possibleMethods = reflectionUtilsType.GetMethods(BindingFlags.Public | BindingFlags.Static)
                    .Where(m => m.Name == "LoadAllMethodsWithAttribute");
                foreach (var m in possibleMethods)
                {
                    var parameters = m.GetParameters();
                    if (parameters.Length != 3)
                        continue;
                    var param = parameters[0];
                    var paramType = param.ParameterType;
                    if (!paramType.IsGenericType)
                        continue;
                    var funcParamCount = paramType.GenericTypeArguments.Length;
                    if (funcParamCount != 4)
                        continue;
                    param = parameters[1];
                    paramType = param.ParameterType;
                    if (paramType != s_MethodSignatureType)
                        continue;
                    mi = m;
                    break;
                }

                if (mi == null)
                    throw new Exception("Could not find LoadAllMethodsWithAttribute method.");

                s_LoadAllMethodsWithAttributeMethodInfo = mi.MakeGenericMethod(new[] { typeof(EditorFilterAttribute), typeof(EditorFilterCreator) });
                if (s_LoadAllMethodsWithAttributeMethodInfo == null)
                    throw new Exception("Could not make generic LoadAllMethodsWithAttribute method.");
            }

            Func<MethodInfo, EditorFilterAttribute, Delegate, EditorFilterCreator> d = (mi, attribute, handler) =>
            {
                if (handler is EditorFilterCreatorHandler efch)
                    return new EditorFilterCreator(attribute.name, efch);
                throw new ArgumentException($"Handler should be of type {typeof(EditorFilterCreatorHandler)}.", nameof(handler));
            };
            var signature = s_MethodSignatureFromDelegateMethodInfo.Invoke(null, new object[] { });
            var allCreators = s_LoadAllMethodsWithAttributeMethodInfo.Invoke(null, new object[] { d, signature, 0 }) as IEnumerable<EditorFilterCreator>;

            m_AllFilters = allCreators.ToDictionary(creator => creator.name, creator => creator.handler());
        }
    }
}
#endif
