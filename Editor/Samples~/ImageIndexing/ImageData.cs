using System;
using System.Text;
using UnityEngine;

namespace UnityEditor.Search
{
    struct ColorInfo
    {
        public uint color;
        public double ratio;

        public override string ToString()
        {
            return $"{ImageUtils.IntToColor32(color)} [{(ratio * 100)}%]";
        }
    }

    interface IHistogram
    {
        int bins { get; }
        int channels { get; }

        float[] GetBins(int channel);
    }

    class Histogram : IHistogram
    {
        public const int histogramSize = 256;

        public virtual int bins => histogramSize;
        public int channels => 3;

        public float[] valuesR;
        public float[] valuesG;
        public float[] valuesB;

        public Histogram()
        {
            valuesR = new float[histogramSize];
            valuesG = new float[histogramSize];
            valuesB = new float[histogramSize];
        }

        public void AddPixel(Color32 pixel)
        {
            ++valuesR[pixel.r];
            ++valuesG[pixel.g];
            ++valuesB[pixel.b];
        }

        public void Normalize(int totalPixels)
        {
            for (var i = 0; i < bins; ++i)
            {
                valuesR[i] /= totalPixels;
                valuesG[i] /= totalPixels;
                valuesB[i] /= totalPixels;
            }
        }

        public void Normalize(int[] totalPixels)
        {
            if (totalPixels.Length != 3)
                throw new ArgumentException($"Array size should be {channels}", nameof(totalPixels));

            for (var i = 0; i < bins; ++i)
            {
                valuesR[i] /= totalPixels[0];
                valuesG[i] /= totalPixels[1];
                valuesB[i] /= totalPixels[2];
            }
        }

        // Combine multiple partial histogram before normalizing
        public void Combine(Histogram histogram)
        {
            for (var i = 0; i < bins; ++i)
            {
                valuesR[i] += histogram.valuesR[i];
                valuesG[i] += histogram.valuesG[i];
                valuesB[i] += histogram.valuesB[i];
            }
        }

        public float[] GetBins(int channel)
        {
            switch (channel)
            {
                case 0:
                    return valuesR;
                case 1:
                    return valuesG;
                case 2:
                    return valuesB;
                default:
                    throw new ArgumentOutOfRangeException(nameof(channel));
            }
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"R: ({string.Join(", ", valuesR)})");
            sb.AppendLine($"G: ({string.Join(", ", valuesG)})");
            sb.AppendLine($"B: ({string.Join(", ", valuesB)})");
            return sb.ToString();
        }
    }

    enum EdgeDirection
    {
        DEG_0 = 0,
        DEG_45 = 1,
        DEG_90 = 2,
        DEG_135 = 3
    }

    class EdgeHistogram : Histogram
    {
        public static readonly int edgeDirections = Enum.GetNames(typeof(EdgeDirection)).Length;

        public override int bins => edgeDirections;

        public EdgeHistogram()
        {
            valuesR = new float[edgeDirections];
            valuesG = new float[edgeDirections];
            valuesB = new float[edgeDirections];
        }

        public void AddEdge(int channel, EdgeDirection direction)
        {
            var values = GetBins(channel);
            ++values[(int)direction];
        }

        public void AddEdge(int channel, float degree)
        {
            var direction = GetDirection(degree);
            AddEdge(channel, direction);
        }

        public static EdgeDirection GetDirection(float degree)
        {
            while (degree < 0)
                degree += 180;
            while (degree > 180)
                degree -= 180;

            var region = Mathf.RoundToInt(degree / 45) % 4;
            return (EdgeDirection)region;
        }
    }

    struct ImageData
    {
        public const int version = 0x03;

        public Hash128 guid;
        public ColorInfo[] bestColors;
        public ColorInfo[] bestShades;
        public Histogram histogram;
        public EdgeHistogram edgeHistogram;
        public double[] edgeDensities;
        public double[] geometricMoments;

        public ImageData(string assetPath)
        {
            guid = Hash128.Compute(assetPath);
            bestColors = new ColorInfo[5];
            bestShades = new ColorInfo[5];
            histogram = new Histogram();
            edgeHistogram = new EdgeHistogram();
            edgeDensities = new double[3];
            geometricMoments = new double[3];
        }

        public ImageData(Hash128 assetGuid)
        {
            guid = assetGuid;
            bestColors = new ColorInfo[5];
            bestShades = new ColorInfo[5];
            histogram = new Histogram();
            edgeHistogram = new EdgeHistogram();
            edgeDensities = new double[3];
            geometricMoments = new double[3];
        }
    }
}
