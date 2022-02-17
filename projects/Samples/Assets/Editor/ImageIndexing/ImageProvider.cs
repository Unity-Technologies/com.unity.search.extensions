#if USE_SEARCH_EXTENSION_API
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using JetBrains.Annotations;
using UnityEditor;
using UnityEngine;

namespace UnityEditor.Search.Providers
{
    class EdgeInfo
    {
        public EdgeHistogram histogram;
        public double[] densities;
    }

    class ImageProvider
    {
        const string k_Type = "img";
        const string k_DisplayName = "Image";

        static List<ImageDatabase> m_ImageIndexes;

        static QueryEngine<ImageData> s_QueryEngine;
        static List<ImageDatabase> indexes
        {
            get
            {
                if (m_ImageIndexes == null)
                {
                    UpdateImageIndexes();
                }

                return m_ImageIndexes;
            }
        }

        [UsedImplicitly, SearchItemProvider]
        internal static SearchProvider CreateProvider()
        {
            return new SearchProvider(k_Type, k_DisplayName)
            {
                filterId = "img:",
                showDetails = true,
                showDetailsOptions = ShowDetailsOptions.Inspector | ShowDetailsOptions.Default,
                fetchItems = (context, items, provider) => SearchItems(context, provider),
                // toObject = (item, type) => GetItemObject(item),
                isExplicitProvider = true,
                fetchDescription = FetchDescription,
                fetchThumbnail = (item, context) => SearchUtils.GetAssetThumbnailFromPath(item.id),
                fetchPreview = (item, context, size, options) => SearchUtils.GetAssetPreviewFromPath(item.id, options),
                // trackSelection = (item, context) => TrackSelection(item),
                // fetchKeywords = FetchKeywords,
                // startDrag = (item, context) => DragItem(item, context),
                onEnable = OnEnable
            };
        }

        static string FetchDescription(SearchItem item, SearchContext context)
        {
            var imageData = (ImageData)item.data;
            var sb = new StringBuilder();
            sb.AppendLine($"Best Color: {imageData.bestColors[0]}");
            sb.AppendLine($"Edge densities: ({string.Join(", ", imageData.edgeDensities)})");
            sb.AppendLine("Edge Histogram:");
            sb.Append($"{imageData.edgeHistogram}");
            sb.AppendLine($"Geometric moments: ({string.Join(", ", imageData.geometricMoments)})");
            return sb.ToString();
        }

        static void OnEnable()
        {
            if (s_QueryEngine == null)
            {
                s_QueryEngine = new QueryEngine<ImageData>();
                s_QueryEngine.AddFilter("color", (imageData, context) => GetColorSimilitude(imageData, context), param => GetColorFromParameter(param));
                s_QueryEngine.AddFilter("hist", (imageData, context) => GetHistogramSimilitude(imageData, context), param => GetHistogramFromParameter(param));
                s_QueryEngine.AddFilter("edge", (imageData, context) => GetEdgeHistogramSimilitude(imageData, context), param => GetEdgeHistogramFromParameter(param));
                s_QueryEngine.AddFilter("density", (imageData, context) => GetEdgeDensitySimilitude(imageData, context), param => GetEdgeHistogramFromParameter(param));
                s_QueryEngine.AddFilter("geom", (imageData, context) => GetGeometricMomentSimilitude(imageData, context), param => GetGeometricMomentFromParameter(param));
                s_QueryEngine.SetSearchDataCallback(DefaultSearchDataCallback);
            }
        }

        static double GetColorSimilitude(ImageData imageData, Color paramColor)
        {
            var similarities = imageData.bestShades.Select(colorInfo =>
            {
                var color32 = ImageUtils.IntToColor32(colorInfo.color);
                var color = ImageUtils.Color32ToColor(color32);
                var ratio = colorInfo.ratio;
                return ImageUtils.WeightedSimilarity(color, ratio, paramColor);
            });
            return similarities.Sum();
        }

        static Color GetColorFromParameter(string param)
        {
            if (param.StartsWith("#"))
            {
                ColorUtility.TryParseHtmlString(Convert.ToString(param), out var color);
                return color;
            }

            switch (param)
            {
                case "red": return Color.red;
                case "green": return Color.green;
                case "blue": return Color.blue;
                case "black": return Color.black;
                case "white": return Color.white;
                case "yellow": return Color.yellow;
            }

            return Color.black;
        }

        static float GetHistogramSimilitude(ImageData imageData, Histogram context)
        {
            if (context == null)
                return 0.0f;
            return 1.0f - ImageUtils.HistogramDistance(imageData.histogram, context, HistogramDistance.MDPA);
        }

        static float GetEdgeHistogramSimilitude(ImageData imageData, EdgeInfo context)
        {
            if (context == null)
                return 0.0f;

            return 1.0f - ImageUtils.HistogramDistance(imageData.edgeHistogram, context.histogram, HistogramDistance.MDPA);
        }

        static float GetEdgeDensitySimilitude(ImageData imageData, EdgeInfo context)
        {
            if (context == null)
                return 0.0f;

            var densitySimilitude = 1.0 - MathUtils.NormalizedDistance(imageData.edgeDensities, context.densities);
            return (float)densitySimilitude;
        }

        static Histogram GetHistogramFromParameter(string param)
        {
            var sanitizedParam = param.Replace("\\", "/");
            var idb = indexes.FirstOrDefault(db => db.ContainsAsset(sanitizedParam));
            if (idb != null)
            {
                var imageData = idb.GetImageDataFromPath(sanitizedParam);
                return imageData.histogram;
            }

            if (!File.Exists(sanitizedParam))
                return null;

            var texture = AssetDatabase.LoadMainAssetAtPath(sanitizedParam) as Texture2D;
            if (texture == null)
                return null;

            Color32[] pixels;
            if (texture.isReadable)
                pixels = texture.GetPixels32();
            else
            {
                var copy = TextureUtils.CopyTextureReadable(texture, texture.width, texture.height);
                pixels = copy.GetPixels32();
            }
            var histogram = new Histogram();
            ImageUtils.ComputeHistogram(pixels, histogram);
            return histogram;
        }

        static EdgeInfo GetEdgeHistogramFromParameter(string param)
        {
            var sanitizedParam = param.Replace("\\", "/");
            var idb = indexes.FirstOrDefault(db => db.ContainsAsset(sanitizedParam));
            if (idb != null)
            {
                var imageData = idb.GetImageDataFromPath(sanitizedParam);
                return new EdgeInfo() { histogram = imageData.edgeHistogram, densities = imageData.edgeDensities };
            }

            if (!File.Exists(sanitizedParam))
                return null;

            var texture = AssetDatabase.LoadMainAssetAtPath(sanitizedParam) as Texture2D;
            if (texture == null)
                return null;

            Color[] pixels = TextureUtils.GetPixels(texture);
            var histogram = new EdgeHistogram();
            var densities = new double[histogram.channels];
            ImageUtils.ComputeEdgesHistogramAndDensity(pixels, texture.width, texture.height, histogram, densities);
            return new EdgeInfo() { histogram = histogram, densities = densities };
        }

        static float GetGeometricMomentSimilitude(ImageData imageData, double[] geoMoments)
        {
            if (geoMoments == null)
                return 0.0f;

            // This is incorrect. The geometric moments are not values that are bound between 0..1
            var geometricSimilitude = 1.0 - MathUtils.NormalizedDistance(imageData.geometricMoments, geoMoments);
            return (float)geometricSimilitude;
        }

        static double[] GetGeometricMomentFromParameter(string param)
        {
            var sanitizedParam = param.Replace("\\", "/");
            var idb = indexes.FirstOrDefault(db => db.ContainsAsset(sanitizedParam));
            if (idb != null)
            {
                var imageData = idb.GetImageDataFromPath(sanitizedParam);
                return imageData.geometricMoments;
            }

            if (!File.Exists(sanitizedParam))
                return null;

            var texture = AssetDatabase.LoadMainAssetAtPath(sanitizedParam) as Texture2D;
            if (texture == null)
                return null;

            var pixels = TextureUtils.GetPixels(texture);
            var geoMoments = new double[3];
            ImageUtils.ComputeSecondOrderInvariant(pixels, texture.width, texture.height, geoMoments);
            return geoMoments;
        }

        static string[] DefaultSearchDataCallback(ImageData data)
        {
            var assetPath = indexes.Select(db => db.GetAssetPath(data.guid)).FirstOrDefault(path => path != null);
            return new[] { assetPath };
        }

        static void UpdateImageIndexes()
        {
            m_ImageIndexes = ImageDatabase.Enumerate().ToList();
        }

        static IEnumerator SearchItems(SearchContext context, SearchProvider provider)
        {
            var searchQuery = context.searchQuery;

            if (searchQuery.Length > 0)
                yield return indexes.Select(db => SearchIndexes(searchQuery, context, provider, db));
        }

        static IEnumerator SearchIndexes(string searchQuery, SearchContext context, SearchProvider provider, ImageDatabase db)
        {
            var query = s_QueryEngine.Parse(searchQuery);

            var filterNodes = GetFilterNodes(query.evaluationGraph);

            var scoreModifiers = new List<Func<ImageData, int, ImageDatabase, int>>();
            foreach (var filterNode in filterNodes)
            {
                switch (filterNode.filterId)
                {
                    case "color":
                    {
                        var paramColor = GetColorFromParameter(filterNode.paramValue);
                        scoreModifiers.Add(GetColorScoreModifier(paramColor));
                        break;
                    }
                    case "hist":
                    {
                        var paramHist = GetHistogramFromParameter(filterNode.paramValue);
                        scoreModifiers.Add(GetHistogramScoreModifier(paramHist));
                        break;
                    }
                    case "edge":
                    {
                        var edgeInfo = GetEdgeHistogramFromParameter(filterNode.paramValue);
                        scoreModifiers.Add(GetEdgeHistogramScoreModifier(edgeInfo));
                        break;
                    }
                    case "density":
                    {
                        var edgeInfo = GetEdgeHistogramFromParameter(filterNode.paramValue);
                        scoreModifiers.Add(GetEdgeDensityScoreModifier(edgeInfo));
                        break;
                    }
                    case "geom":
                    {
                        var moments = GetGeometricMomentFromParameter(filterNode.paramValue);
                        scoreModifiers.Add(GetGeometricMomentsScoreModifier(moments));
                        break;
                    }
                }
            }

            var filteredImageData = query.Apply(db.imagesData);
            foreach (var data in filteredImageData)
            {
                var score = 0;
                foreach (var scoreModifier in scoreModifiers)
                {
                    score = scoreModifier(data, score, db);
                }

                var assetPath = db.GetAssetPath(data.guid);
                var name = Path.GetFileNameWithoutExtension(assetPath);

                yield return provider.CreateItem(context, assetPath, score, name, null, null, data);
            }
        }

        static IEnumerable<IFilterNode> GetFilterNodes(QueryGraph graph)
        {
            return EnumerateNodes(graph).Where(node => node.type == QueryNodeType.Filter).Select(node => node as IFilterNode);
        }

        static Func<ImageData, int, ImageDatabase, int> GetColorScoreModifier(Color searchedColor)
        {
            return (imageData, inputScore, db) =>
            {
                // var assetPath = db.GetAssetPath(imageData.guid);
                // var color32 = ImageUtils.IntToColor32(imageData.bestShades[0].color);
                // var color = ImageUtils.Color32ToColor(color32);
                // Debug.Log($"{assetPath}: {ImageUtils.CIELabDistance(color, searchedColor)}");
                var similitude = GetColorSimilitude(imageData, searchedColor);
                return inputScore - (int)(similitude * 100);
            };
        }

        static Func<ImageData, int, ImageDatabase, int> GetHistogramScoreModifier(Histogram searchedHistogram)
        {
            return (imageData, inputScore, db) =>
            {
                var similitude = GetHistogramSimilitude(imageData, searchedHistogram);
                return inputScore - (int)(similitude * 100);
            };
        }

        static Func<ImageData, int, ImageDatabase, int> GetEdgeHistogramScoreModifier(EdgeInfo searchedEdgeInfo)
        {
            return (imageData, inputScore, db) =>
            {
                var similitude = GetEdgeHistogramSimilitude(imageData, searchedEdgeInfo);
                return inputScore - (int)(similitude * 100);
            };
        }

        static Func<ImageData, int, ImageDatabase, int> GetEdgeDensityScoreModifier(EdgeInfo searchedEdgeInfo)
        {
            return (imageData, inputScore, db) =>
            {
                var similitude = GetEdgeDensitySimilitude(imageData, searchedEdgeInfo);
                return inputScore - (int)(similitude * 100);
            };
        }

        static Func<ImageData, int, ImageDatabase, int> GetGeometricMomentsScoreModifier(double[] moments)
        {
            return (imageData, inputScore, db) =>
            {
                var similitude = GetGeometricMomentSimilitude(imageData, moments);
                return inputScore - (int)(similitude * 100);
            };
        }

        internal static IEnumerable<IQueryNode> EnumerateNodes(QueryGraph graph)
        {
            if (graph.empty)
                yield break;

            var nodeStack = new Stack<IQueryNode>();
            nodeStack.Push(graph.root);

            while (nodeStack.Count > 0)
            {
                var currentNode = nodeStack.Pop();
                if (!currentNode.leaf && currentNode.children != null)
                {
                    foreach (var child in currentNode.children)
                    {
                        nodeStack.Push(child);
                    }
                }

                yield return currentNode;
            }
        }
    }
}
#endif