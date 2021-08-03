using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace UnityEditor.Search
{
    [ExcludeFromPreset]
    class ImageDatabase : ScriptableObject
    {
        public const int version = 0x01 << 16 | ImageData.version;

        Dictionary<Hash128, string> m_HashToFileMap = new Dictionary<Hash128, string>();
        Dictionary<string, int> m_AssetPathToIndex = new Dictionary<string, int>();

        [SerializeField, HideInInspector] public byte[] bytes;

        public List<ImageData> imagesData { get; set; } = new List<ImageData>();

        internal void OnEnable()
        {
            if (bytes == null)
                bytes = new byte[0];
            else
            {
                if (bytes.Length > 0)
                    Load();
            }
        }

        public string GetAssetPath(Hash128 hash)
        {
            if (m_HashToFileMap.TryGetValue(hash, out var path))
                return path;
            return null;
        }

        public bool ContainsAsset(string assetPath)
        {
            return m_AssetPathToIndex.ContainsKey(assetPath);
        }

        public ImageData GetImageDataFromPath(string assetPath)
        {
            if (!m_AssetPathToIndex.TryGetValue(assetPath, out var index))
                throw new Exception("Asset path not found.");
            return imagesData[index];
        }

        public void IndexTexture(string assetPath, Texture2D texture)
        {
            var imageIndexData = new ImageData(assetPath);
            Color32[] pixels32 = TextureUtils.GetPixels32(texture);
            Color[] pixels = TextureUtils.GetPixels(texture);

            IndexColors(imageIndexData, pixels32);
            IndexShapes(imageIndexData, pixels32, pixels, texture.width, texture.height);
            imagesData.Add(imageIndexData);
            m_HashToFileMap.Add(imageIndexData.guid, assetPath);
        }

        public static IEnumerable<ImageDatabase> Enumerate()
        {
            const string imageDataFindAssetQuery = "t:ImageDatabase a:all";
            return AssetDatabase.FindAssets(imageDataFindAssetQuery).Select(AssetDatabase.GUIDToAssetPath)
                .Select(path => AssetDatabase.LoadAssetAtPath<ImageDatabase>(path));
        }

        public void WriteBytes()
        {
            using (var ms = new MemoryStream())
            {
                WriteTo(ms);
                bytes = ms.ToArray();
            }
        }

        void WriteTo(Stream s)
        {
            using (var bw = new BinaryWriter(s))
            {
                // Hashes
                bw.Write(m_HashToFileMap.Count);
                foreach (var kvp in m_HashToFileMap)
                {
                    bw.Write(kvp.Key.ToString());
                    bw.Write(kvp.Value);
                }

                // Images data
                bw.Write(imagesData.Count);
                foreach (var imageData in imagesData)
                {
                    // Guid
                    bw.Write(imageData.guid.ToString());

                    // Best colors
                    bw.Write(imageData.bestColors.Length);
                    foreach (var bestColor in imageData.bestColors)
                    {
                        bw.Write(bestColor.color);
                        bw.Write(bestColor.ratio);
                    }

                    // Best shades
                    bw.Write(imageData.bestShades.Length);
                    foreach (var bestShade in imageData.bestShades)
                    {
                        bw.Write(bestShade.color);
                        bw.Write(bestShade.ratio);
                    }

                    // Color histogram
                    WriteHistogram(imageData.histogram.valuesR, bw);
                    WriteHistogram(imageData.histogram.valuesG, bw);
                    WriteHistogram(imageData.histogram.valuesB, bw);

                    // Edges histogram
                    WriteHistogram(imageData.edgeHistogram.valuesR, bw);
                    WriteHistogram(imageData.edgeHistogram.valuesG, bw);
                    WriteHistogram(imageData.edgeHistogram.valuesB, bw);

                    // Edge density
                    WriteArrayDouble(imageData.edgeDensities, bw);

                    // Geometric moments
                    WriteArrayDouble(imageData.geometricMoments, bw);
                }
            }
        }

        static void WriteHistogram(float[] histogram, BinaryWriter bw)
        {
            foreach (var f in histogram)
            {
                bw.Write(f);
            }
        }

        static void WriteArrayDouble(double[] array, BinaryWriter bw)
        {
            bw.Write(array.Length);
            foreach (var value in array)
            {
                bw.Write(value);
            }
        }

        void Load()
        {
            using (var ms = new MemoryStream(bytes))
            {
                ReadFrom(ms);
            }
        }

        void ReadFrom(Stream s)
        {
            using (var br = new BinaryReader(s))
            {
                // Hashes
                var hashCount = br.ReadInt32();
                m_HashToFileMap = new Dictionary<Hash128, string>();
                for (var i = 0; i < hashCount; ++i)
                {
                    var hash = Hash128.Parse(br.ReadString());
                    var value = br.ReadString();

                    m_HashToFileMap[hash] = value;
                }

                // Images data
                var imageDataCount = br.ReadInt32();
                imagesData = new List<ImageData>(imageDataCount);
                m_AssetPathToIndex = new Dictionary<string, int>();
                for (var i = 0; i < imageDataCount; ++i)
                {
                    // Guid
                    var guid = Hash128.Parse(br.ReadString());
                    var imageData = new ImageData(guid);

                    // Best colors
                    var nbColor = br.ReadInt32();
                    imageData.bestColors = new ColorInfo[nbColor];
                    for (var j = 0; j < nbColor; ++j)
                    {
                        var colorInfo = new ColorInfo();
                        colorInfo.color = br.ReadUInt32();
                        colorInfo.ratio = br.ReadDouble();
                        imageData.bestColors[j] = colorInfo;
                    }

                    // Best shades
                    var nbShades = br.ReadInt32();
                    imageData.bestShades = new ColorInfo[nbShades];
                    for (var j = 0; j < nbShades; ++j)
                    {
                        var colorInfo = new ColorInfo();
                        colorInfo.color = br.ReadUInt32();
                        colorInfo.ratio = br.ReadDouble();
                        imageData.bestShades[j] = colorInfo;
                    }

                    // Color histogram
                    ReadHistogram(imageData.histogram, br);

                    // Edges histogram
                    ReadHistogram(imageData.edgeHistogram, br);

                    // Edge densities
                    imageData.edgeDensities = ReadArrayDouble(br);

                    // Geometric moments
                    imageData.geometricMoments = ReadArrayDouble(br);

                    imagesData.Add(imageData);

                    var assetPath = m_HashToFileMap[guid];
                    m_AssetPathToIndex.Add(assetPath, i);
                }
            }
        }

        static void ReadHistogram(IHistogram histogram, BinaryReader br)
        {
            for (var c = 0; c < histogram.channels; ++c)
            {
                var bins = histogram.GetBins(c);
                for (var i = 0; i < histogram.bins; ++i)
                {
                    bins[i] = br.ReadSingle();
                }
            }
        }

        static double[] ReadArrayDouble(BinaryReader br)
        {
            var nbValues = br.ReadInt32();
            var array = new double[nbValues];
            for (var j = 0; j < nbValues; ++j)
            {
                array[j] = br.ReadDouble();
            }

            return array;
        }

        static void IndexColors(ImageData imageData, Color32[] pixels)
        {
            ImageUtils.ComputeBestColorsAndHistogram(pixels, imageData.bestColors, imageData.bestShades, imageData.histogram);
        }

        static void IndexShapes(ImageData imageData, Color32[] pixels32, Color[] pixels, int width, int height)
        {
            ImageUtils.ComputeEdgesHistogramAndDensity(pixels, width, height, imageData.edgeHistogram, imageData.edgeDensities);
            ImageUtils.ComputeFirstOrderInvariant(pixels, width, height, imageData.geometricMoments);
        }
    }
}
