using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace UnityEditor.Search
{
    struct ScaleInfo
    {
        public float sigma;
        public int scale;
    }

    struct ExtremaInfo
    {
        public PixelPosition position;
        public ScaleInfo scaleInfo;
        public ImagePixels source;
    }

    class Octave
    {
        public List<ImagePixels> gaussians;
        public List<ImagePixels> diffOfGauss;
        public List<ExtremaInfo> extremas;

        public Octave(List<ImagePixels> gaussians, List<ImagePixels> dogs)
        {
            this.gaussians = gaussians;
            this.diffOfGauss = dogs;
        }

        public ImagePixels GetDoGByScale(int scale)
        {
            if (scale < 0 || scale >= Sift.octaveSize)
                throw new ArgumentOutOfRangeException(nameof(scale), $"Scale ({scale}) is outside the range of possible values ([{0}, {Sift.octaveSize}[).");

            return diffOfGauss[scale];
        }

        public bool WithinOffset(int x, int y, int offset)
        {
            var width = diffOfGauss[0].width;
            var height = diffOfGauss[0].height;

            if (x < offset || x + offset >= width ||
                y < offset || y + offset >= height)
                return false;
            return true;
        }
    }

    struct QuadraticInterpolation
    {
        public Vector3 offset;
        public float interpolatedPixelValue;
    }

    class Sift
    {
        const float k_InitialSigma = 1.6f;
        const int k_Intervals = 2;
        public const int octaveSize = k_Intervals + 3;
        const int k_NumberOfUsableScale = k_Intervals;
        const int k_OctaveCount = 4;
        static readonly float k_ScaleFactor = MathF.Sqrt(2); //MathF.Pow(2, 1f / k_Intervals);
        const float k_ContrastThreshold = 0.03f;
        const int k_MaxInterpolationSteps = 5;
        const float k_InterpolationOffsetThreshold = 0.5f;
        const float k_EdgeRatio = 10f;
        const float k_EdgeThreshold = (k_EdgeRatio + 1)*(k_EdgeRatio + 1) / (k_EdgeRatio);

        ImagePixels m_Image;

        Dictionary<ImagePixels, ScaleInfo> m_ScaleInfos;

        List<Octave> m_Octaves;
        public List<Octave> octaves => m_Octaves;

        public IEnumerable<ExtremaInfo> extremas => m_Octaves.SelectMany(octave => octave.extremas);

        public Sift(ImagePixels image)
        {
            m_Image = ImageUtils.ToGrayScale(image);
            m_ScaleInfos = new Dictionary<ImagePixels, ScaleInfo>();
        }

        public void Compute()
        {
            m_Octaves = new List<Octave>();
            for (var currentOctave = 0; currentOctave < k_OctaveCount; ++currentOctave)
            {
                var previousOctave = m_Octaves.LastOrDefault();
                var octave = ComputeOctave(previousOctave);
                m_Octaves.Add(octave);
            }
        }

        Octave ComputeOctave(Octave previousOctave)
        {
            var gaussians = new List<ImagePixels>();
            var dogs = new List<ImagePixels>();
            ImagePixels source = null;
            var sigma = k_InitialSigma;
            if (previousOctave == null)
            {
                var kernelSize = GaussianFilter.GetSizeFromSigma(k_InitialSigma);
                var gaussFilter = new GaussianFilter(kernelSize, k_InitialSigma);
                source = m_Image;
                var firstGauss = gaussFilter.Apply(m_Image);
                m_ScaleInfos.TryAdd(firstGauss, new ScaleInfo() { scale = 0, sigma = k_InitialSigma });
            }
            else
            {
                if (previousOctave.gaussians.Count != octaveSize)
                    throw new Exception("Octave did not have the correct amount of images.");
                var doubleSigmaImage = previousOctave.gaussians[2];
                if (!m_ScaleInfos.TryGetValue(doubleSigmaImage, out var doubleSigmaInfo))
                    throw new Exception("Gaussian image should be present in the DoG infos.");
                source = ImageUtils.DownSample(doubleSigmaImage, 2);
                sigma = doubleSigmaInfo.sigma;
                m_ScaleInfos.TryAdd(source, new ScaleInfo() { scale = 0, sigma = doubleSigmaInfo.sigma });
            }
            gaussians.Add(source);

            // Compute the gaussians and the difference of gaussians
            for (var i = 1; i < octaveSize; ++i)
            {
                var previousSigma = sigma;
                sigma *= k_ScaleFactor;
                var kernelSize = GaussianFilter.GetSizeFromSigma(sigma);
                var gaussFilter = new GaussianFilter(kernelSize, sigma);
                var currentGaussian = gaussFilter.Apply(source);
                gaussians.Add(currentGaussian);
                m_ScaleInfos.TryAdd(currentGaussian, new ScaleInfo() { scale = i, sigma = sigma });

                var dog = Filtering.Subtract(currentGaussian, gaussians[i - 1]);
                dog = ImageUtils.StretchImage(dog, 0, 1);
                dogs.Add(dog);
                m_ScaleInfos.TryAdd(dog, new ScaleInfo() { scale = i - 1, sigma = previousSigma });
            }

            var octave = new Octave(gaussians, dogs);

            // Find the extremas in the difference of gaussians
            var extremas = FilterExtremas(FindExtremas(dogs), octave, (ei, o) => CheckContrast(ei, o, 0.8f));
            extremas = InterpolateAndFilterKeypoints(extremas, octave);

            octave.extremas = extremas.ToList();
            return octave;
        }

        List<ExtremaInfo> FindExtremas(List<ImagePixels> dogs)
        {
            if (dogs.Count < 3)
                throw new Exception("Should have at least 3 difference of gaussians.");

            var allExtremas = new List<ExtremaInfo>();
            for (var i = 1; i < dogs.Count - 1; ++i)
            {
                var extremas = FindExtremas(dogs[i - 1], dogs[i], dogs[i + 1]);
                allExtremas.AddRange(extremas);
            }

            return allExtremas;
        }

        List<ExtremaInfo> FindExtremas(ImagePixels previousScale, ImagePixels currentScale, ImagePixels nextScale)
        {
            if (previousScale.width != currentScale.width || previousScale.width != nextScale.width || previousScale.height != currentScale.height || previousScale.height != nextScale.height)
                throw new Exception("Difference of gaussians should have the same size to find extremas.");

            var extremas = new List<ExtremaInfo>();
            for (var row = 1; row < currentScale.height - 1; ++row)
            {
                for (var col = 1; col < currentScale.width - 1; ++col)
                {
                    var currentPixel = currentScale[row, col];
                    var neighborhood = ImageUtils.GetNeighborhood(previousScale, col, row, true)
                        .Concat(ImageUtils.GetNeighborhood(currentScale, col, row, false))
                        .Concat(ImageUtils.GetNeighborhood(nextScale, col, row, true));

                    var minValue = float.MaxValue;
                    var maxValue = float.MinValue;
                    foreach (var neighbor in neighborhood)
                    {
                        if (neighbor.r < minValue)
                            minValue = neighbor.r;
                        if (neighbor.r > maxValue)
                            maxValue = neighbor.r;
                    }

                    if (currentPixel.r > maxValue || currentPixel.r < minValue)
                    {
                        extremas.Add(new ExtremaInfo() { position = new PixelPosition(col, row, currentScale), scaleInfo = m_ScaleInfos[currentScale], source = currentScale });
                    }
                }
            }

            return extremas;
        }

        Matrix3x3 Hessian3D(PixelPosition point, Octave octave)
        {
            int scale = m_ScaleInfos[point.source].scale;
            var x = point.x;
            var y = point.y;

            var prev = octave.diffOfGauss[scale - 1];
            var img = octave.diffOfGauss[scale];
            var next = octave.diffOfGauss[scale + 1];

            var subtract = 2 * img[y, x].r;

            var dxx = (img[y, x + 1].r + img[y, x - 1].r - subtract);
            var dyy = (img[y + 1, x].r + img[y - 1, x].r - subtract);
            var dss = (next[y, x].r + prev[y, x].r - subtract);
            var dxy = (img[y + 1, x + 1].r - img[y + 1, x - 1].r -
                img[y - 1, x + 1].r + img[y - 1, x - 1].r) * 0.25f;
            var dxs = (next[y, x + 1].r - next[y, x - 1].r -
                prev[y, x + 1].r + prev[y, x - 1].r) * 0.25f;
            var dys = (next[y + 1, x].r - next[y - 1, x].r -
                prev[y + 1, x].r + prev[y - 1, x].r) * 0.25f;

            return new Matrix3x3(new[,]
            {
                {dxx, dxy, dxs},
                {dxy, dyy, dys},
                {dxs, dys, dss}
            });
        }

        Matrix2x2 Hessian2D(PixelPosition point, Octave octave)
        {
            int scale = m_ScaleInfos[point.source].scale;
            var x = point.x;
            var y = point.y;

            var img = octave.diffOfGauss[scale];

            var subtract = 2 * img[y, x].r;

            var dxx = (img[y, x + 1].r + img[y, x - 1].r - subtract);
            var dyy = (img[y + 1, x].r + img[y - 1, x].r - subtract);
            var dxy = (img[y + 1, x + 1].r - img[y + 1, x - 1].r -
                img[y - 1, x + 1].r + img[y - 1, x - 1].r) * 0.25f;

            return new Matrix2x2(new float[,]
            {
                { dxx, dxy },
                { dxy, dyy }
            });
        }

        Vector3 Gradient(PixelPosition point, Octave octave)
        {
            int scale = m_ScaleInfos[point.source].scale;
            var x = point.x;
            var y = point.y;
            var prev = octave.diffOfGauss[scale - 1];
            var img = octave.diffOfGauss[scale];
            var next = octave.diffOfGauss[scale + 1];

            return new Vector3((img[y, x + 1].r - img[y, x - 1].r) * 0.5f,
            (img[y + 1, x].r - img[y - 1, x].r) * 0.5f,
            (next[y, x].r - prev[y, x].r) * 0.5f);
        }

        QuadraticInterpolation? Interpolate(PixelPosition point, Octave octave)
        {
            int scale = m_ScaleInfos[point.source].scale;
            var x = point.x;
            var y = point.y;

            var invHessian = Hessian3D(point, octave).Invert();
            if (!invHessian.HasValue)
                return null;
            var grad = Gradient(point, octave);
            var offset = invHessian.Value * grad;
            var interpolatedValue = octave.diffOfGauss[scale][y, x].r + 0.5f * Vector3.Dot(grad, offset);

            return new QuadraticInterpolation() { offset = offset * -1, interpolatedPixelValue = interpolatedValue };
        }

        IEnumerable<ExtremaInfo> InterpolateAndFilterKeypoints(IEnumerable<ExtremaInfo> extremas, Octave octave)
        {
            foreach (var extremaInfo in extremas)
            {
                var point = extremaInfo.position;
                int scale = m_ScaleInfos[point.source].scale;
                for (var step = 0; step < k_MaxInterpolationSteps; ++step)
                {
                    var q = Interpolate(point, octave);
                    if (!q.HasValue)
                        break;

                    var offset = q.Value.offset;
                    var newScale = scale + offset[2];
                    var newScaleInt = (int)MathF.Round(newScale);
                    var newX = point.x + offset[0];
                    var newXInt = (int)MathF.Round(newX);
                    var newY = point.y + offset[1];
                    var newYInt = (int)MathF.Round(newY);

                    if (!WithinUsableScale(newScaleInt) || !octave.WithinOffset(newXInt, newYInt, 1))
                        break;

                    var newDogSource = octave.GetDoGByScale(newScaleInt);
                    point = new PixelPosition(newXInt, newYInt, newDogSource);
                    if (!InterpolationOffsetWithinThreshold(offset))
                        continue;

                    // Drop low contrast points
                    if (MathF.Abs(q.Value.interpolatedPixelValue) < k_ContrastThreshold)
                        break;

                    // Drop points along edges
                    var h2 = Hessian2D(point, octave);
                    var trace = h2.Trace();
                    var det = h2.Det();
                    if (det == 0 || trace / det >= k_EdgeThreshold)
                        break;

                    yield return new ExtremaInfo()
                    {
                        position = point,
                        scaleInfo = m_ScaleInfos[newDogSource],
                        source = newDogSource
                    };
                }
            }
        }

        bool InterpolationOffsetWithinThreshold(Vector3 offset)
        {
            return offset[0] <= k_InterpolationOffsetThreshold && offset[1] <= k_InterpolationOffsetThreshold && offset[2] <= k_InterpolationOffsetThreshold;
        }

        IEnumerable<ExtremaInfo> FilterExtremas(IEnumerable<ExtremaInfo> extremas, Octave octave, Func<ExtremaInfo, Octave, bool> filterFunc)
        {
            foreach (var extrema in extremas)
            {
                if (filterFunc(extrema, octave))
                    yield return extrema;
            }
        }

        static bool CheckContrast(ExtremaInfo ei, Octave octave, float factor)
        {
            var point = ei.position;
            return point.pixelValue.r >= factor * k_ContrastThreshold;
        }

        static bool WithinUsableScale(int scale)
        {
            return scale is >= 1 and <= k_NumberOfUsableScale;
        }
    }
}
