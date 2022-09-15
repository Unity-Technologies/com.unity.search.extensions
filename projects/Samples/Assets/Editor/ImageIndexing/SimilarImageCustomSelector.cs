using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.Search.Providers;
using UnityEditor.SearchService;

namespace UnityEditor.Search
{
    public class SimilarImageCustomSelector
    {
        const string k_SelectorId = "similar_image_selector";

        [AdvancedObjectSelectorValidator(k_SelectorId)]
        static bool ValidateSelector(ObjectSelectorSearchContext context)
        {
            if (context.requiredTypes == null || context.requiredTypeNames == null)
                return false;

            var currentSelection = context.currentObject;
            var assetPath = AssetDatabase.GetAssetPath(currentSelection);
            if (string.IsNullOrEmpty(assetPath))
                return false;

            if (context.editedObjects == null || context.editedObjects.Length == 0 || context.editedObjects.All(o => o == null))
                return false;

            var requiredTypes = context.requiredTypes.ToList();
            var requiredTypeNames = context.requiredTypeNames.ToList();
            if (requiredTypes.Count != requiredTypeNames.Count)
                return false;

            for (var i = 0; i < requiredTypes.Count; ++i)
            {
                var requiredType = requiredTypes[i];
                var requiredTypeName = requiredTypeNames[i];
                if (requiredType == null && !string.IsNullOrEmpty(requiredTypeName))
                {
                    requiredType = TypeCache.GetTypesDerivedFrom<UnityEngine.Object>().FirstOrDefault(t => t.Name == requiredTypeName);
                    requiredTypes[i] = requiredType ?? throw new ArgumentNullException(nameof(requiredType));
                }
            }

            return requiredTypes.All(ImageDatabaseImporter.IsSupportedType);
        }

        [AdvancedObjectSelector(k_SelectorId, "Similar Image Selector", 0)]
        static void OpenSelector(AdvancedObjectSelectorEventType eventType, in AdvancedObjectSelectorParameters parameters)
        {
            if (eventType != AdvancedObjectSelectorEventType.OpenAndSearch)
                return;

            var selectContext = parameters.context;
            var currentSelection = parameters.context.currentObject;
            var assetPath = AssetDatabase.GetAssetPath(currentSelection);
            if (string.IsNullOrEmpty(assetPath))
                return;

            var sanitizedPath = StringUtils.SanitizePath(assetPath);

            var filters = ImageProvider.ImageEngineFiltersData.Where(d => d.engineFilter.type == ImageEngineFilterType.Binary).Select(d =>
            {
                var newFilter = d.engineFilter;
                newFilter.param = sanitizedPath;
                return newFilter;
            });

            var query = BuildInitialQuery(selectContext, filters);

            var selectHandler = parameters.selectorClosedHandler;
            var trackingHandler = parameters.trackingHandler;
            var viewFlags = SearchFlags.OpenPicker | SearchFlags.Sorted;
            var viewState = new SearchViewState(
                SearchService.CreateContext(ImageProvider.ProviderId, query, viewFlags), selectHandler, trackingHandler,
                selectContext.requiredTypeNames.First(), selectContext.requiredTypes.First());
            viewState.group = ImageProvider.ProviderId;
            viewState.ignoreSaveSearches = true;
            SearchService.ShowPicker(viewState);
        }

        static string BuildInitialQuery(in ObjectSelectorSearchContext selectContext, IEnumerable<ImageEngineFilter> filters)
        {
            var query = ImageProvider.BuildQueryForImage(filters);
            var types = selectContext.requiredTypes.ToArray();
            var typeNames = selectContext.requiredTypeNames.ToArray();
            for (int i = 0; i < types.Length; ++i)
            {
                var name = types[i]?.Name ?? typeNames[i];
                if (query.Length != 0)
                    query += ' ';
                query += $"t:{name}";
            }
            return query;
        }
    }
}
