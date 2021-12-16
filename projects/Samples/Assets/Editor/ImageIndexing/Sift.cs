// #define USE_PARALLEL_SIFT
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

    struct KeyPoint
    {
        public PixelPosition position;
        public ScaleInfo scaleInfo;
        public ImagePixels source;
        public float actualSigma;
        public float magnitude;
        public float orientation;
    }

    class Octave
    {
        Sift m_SiftReft;

        public List<ImagePixels> gaussians;
        public List<ImagePixels> diffOfGauss;
        public List<KeyPoint> keyPoints;

        public float startingSigma => m_SiftReft.scaleInfos[gaussians[0]].sigma;

        public Octave(List<ImagePixels> gaussians, List<ImagePixels> dogs, Sift siftRef)
        {
            this.gaussians = gaussians;
            this.diffOfGauss = dogs;
            this.m_SiftReft = siftRef;
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
        const int k_PixelBorder = 1;
        static readonly float k_InitialSigma = MathF.Sqrt(2); //1.6f;
        const int k_Intervals = 2;
        public const int octaveSize = k_Intervals + 3;
        const int k_NumberOfUsableScale = k_Intervals;
        const int k_OctaveCount = 4;
        static readonly float k_ScaleFactor = MathF.Pow(2, 1f / k_Intervals);
        const float k_ContrastThreshold = 0.015f;
        const int k_MaxInterpolationSteps = 5;
        const float k_InterpolationOffsetThreshold = 0.5f;
        const float k_EdgeRatio = 10f;
        const float k_EdgeThreshold = (k_EdgeRatio + 1)*(k_EdgeRatio + 1) / (k_EdgeRatio);
        const int k_HistogramBinCount = 36;
        const float k_HistogramAngleToBin = k_HistogramBinCount / 360f;
        const float k_HistogramBinToAngle = 360f / k_HistogramBinCount;
        const float k_OrientationWindowFactor = 1.5f;
        const float k_OrientationPeakThresholdFactor = 0.8f;
        const float k_OrientationAngleEpsilon = 0.0001f;

        ImagePixels m_Image;

        public Dictionary<ImagePixels, ScaleInfo> scaleInfos { get; }

        List<Octave> m_Octaves;
        public List<Octave> octaves => m_Octaves;

        public IEnumerable<KeyPoint> keyPoints => m_Octaves.SelectMany(octave => octave.keyPoints);

        public Sift(ImagePixels image)
        {
            var source = image;
            var minSize = Math.Min(image.width, image.height);
            if (minSize < 256)
                source = ImageUtils.UpSample(image, Mathf.CeilToInt(256f / minSize));
            m_Image = ImageUtils.ToGrayScale(source);
            scaleInfos = new Dictionary<ImagePixels, ScaleInfo>();
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
                scaleInfos.TryAdd(firstGauss, new ScaleInfo() { scale = 0, sigma = k_InitialSigma });
                gaussians.Add(firstGauss);
            }
            else
            {
                if (previousOctave.gaussians.Count != octaveSize)
                    throw new Exception("Octave did not have the correct amount of images.");
                var doubleSigmaImage = previousOctave.gaussians[2];
                if (!scaleInfos.TryGetValue(doubleSigmaImage, out var doubleSigmaInfo))
                    throw new Exception("Gaussian image should be present in the DoG infos.");
                source = ImageUtils.DownSample(doubleSigmaImage, 2);
                sigma = doubleSigmaInfo.sigma;
                scaleInfos.TryAdd(source, new ScaleInfo() { scale = 0, sigma = doubleSigmaInfo.sigma });
                gaussians.Add(source);
            }

            // Compute the gaussians and the difference of gaussians
            for (var i = 1; i < octaveSize; ++i)
            {
                var previousSigma = sigma;
                sigma *= k_ScaleFactor;
                var kernelSize = GaussianFilter.GetSizeFromSigma(sigma);
                var gaussFilter = new GaussianFilter(kernelSize, sigma);
                var currentGaussian = gaussFilter.Apply(source);
                gaussians.Add(currentGaussian);
                scaleInfos.TryAdd(currentGaussian, new ScaleInfo() { scale = i, sigma = sigma });

                var dog = Filtering.Subtract(currentGaussian, gaussians[i - 1]);
                // dog = ImageUtils.StretchImage(dog, 0, 1);
                dogs.Add(dog);
                scaleInfos.TryAdd(dog, new ScaleInfo() { scale = i - 1, sigma = previousSigma });
            }

            var octave = new Octave(gaussians, dogs, this);

            // Find the extremas in the difference of gaussians
            var extremas = FindExtremas(dogs);

            // Interpolate the extremas to find the real keypoints, and filter them.
            extremas = InterpolateAndFilterKeyPoints(extremas, octave);

            extremas = FindKeyPointsOrientation(extremas, octave);

            octave.keyPoints = extremas.ToList();
            return octave;
        }

        IEnumerable<KeyPoint> FindExtremas(List<ImagePixels> dogs)
        {
            if (dogs.Count < 3)
                throw new Exception("Should have at least 3 difference of gaussians.");

            var allExtremas = new List<KeyPoint>();
            for (var i = 1; i < dogs.Count - 1; ++i)
            {
                var extremas = FindExtremas(dogs[i - 1], dogs[i], dogs[i + 1]);
                allExtremas.AddRange(extremas);
            }

            return allExtremas;
        }

        IEnumerable<KeyPoint> FindExtremas(ImagePixels previousScale, ImagePixels currentScale, ImagePixels nextScale)
        {
            if (previousScale.width != currentScale.width || previousScale.width != nextScale.width || previousScale.height != currentScale.height || previousScale.height != nextScale.height)
                throw new Exception("Difference of gaussians should have the same size to find extremas.");

            var extremas = new List<KeyPoint>();

#if USE_PARALLEL_SIFT
            var result = ThreadUtils.ParallelForAggregate(k_PixelBorder, currentScale.height - k_PixelBorder,() => new List<KeyPoint>(), (start, end, parallelLoopState, localExtremas) =>
            {
#else
            var start = k_PixelBorder;
            var end = currentScale.height - k_PixelBorder;
            var localExtremas = extremas;
#endif
                for (var row = start; row < end; ++row)
                {
                    for (var col = k_PixelBorder; col < currentScale.width - k_PixelBorder; ++col)
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
                            localExtremas.Add(new KeyPoint() { position = new PixelPosition(col, row, currentScale), scaleInfo = scaleInfos[currentScale], source = currentScale });
                        }
                    }
                }

#if USE_PARALLEL_SIFT
                return localExtremas;
            }, localKeyPoints =>
            {
                lock (extremas)
                {
                    extremas.AddRange(localKeyPoints);
                }
            });

            if (!result.IsCompleted)
                Debug.LogError("FindExtremas did not complete successfully.");
#endif

            return extremas;
        }

        Matrix3x3 Hessian3D(PixelPosition point, Octave octave)
        {
            int scale = scaleInfos[point.source].scale;
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
            int scale = scaleInfos[point.source].scale;
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
            int scale = scaleInfos[point.source].scale;
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
            int scale = scaleInfos[point.source].scale;
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

        IEnumerable<KeyPoint> InterpolateAndFilterKeyPoints(IEnumerable<KeyPoint> _keyPoints, Octave octave)
        {
            foreach (var keyPoint in _keyPoints)
            {
                var point = keyPoint.position;
                int scale = scaleInfos[point.source].scale;
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

                    if (!WithinUsableScale(newScaleInt) || !octave.WithinOffset(newXInt, newYInt, k_PixelBorder))
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

                    yield return new KeyPoint()
                    {
                        position = point,
                        scaleInfo = scaleInfos[newDogSource],
                        source = newDogSource,
                        actualSigma = octave.startingSigma * MathF.Pow(2, newScale / k_Intervals)
                    };
                }
            }
        }

        static IEnumerable<KeyPoint> FindKeyPointsOrientation(IEnumerable<KeyPoint> _keyPoints, Octave octave)
        {
            foreach (var keyPoint in _keyPoints)
            {
                foreach (var kp in FindKeyPointOrientation(keyPoint, octave))
                {
                    yield return kp;
                }
            }
        }

        static IEnumerable<KeyPoint> FindKeyPointOrientation(KeyPoint keyPoint, Octave octave)
        {
            var realSigma = keyPoint.actualSigma;
            var scale = keyPoint.scaleInfo.scale;
            var position = keyPoint.position;
            var gaussian = octave.gaussians[scale];

            var radius = GaussianFilter.GetRadiusFromSigma(realSigma * k_OrientationWindowFactor);
            var kernelSize = 2 * radius + 1;
            var length = kernelSize * kernelSize;
            var kernel = GaussianFilter.Get2DKernel(kernelSize, realSigma);

            var magnitudes = new float[length];
            var orientations = new float[length];
            var histogram = Enumerable.Repeat(0f, k_HistogramBinCount).ToArray();
            int index = 0;
            for (var offsetY = -radius; offsetY <= radius; ++offsetY)
            {
                var y = position.y + offsetY;
                if (!octave.WithinOffset(1, y, 1))
                    continue;
                for (var offsetX = -radius; offsetX <= radius; ++offsetX)
                {
                    var x = position.x + offsetX;
                    if (!octave.WithinOffset(x, y, 1))
                        continue;
                    var dx = gaussian[y, x + 1].r - gaussian[y, x - 1].r;
                    var dy = gaussian[y - 1, x].r - gaussian[y + 1, x].r;

                    var mag = MathF.Sqrt(dx * dx + dy * dy) * (float)kernel[index];
                    var ori = MathF.Atan2(dy, dx);

                    var bin = (int)(MathF.Round(k_HistogramAngleToBin * ori) * Mathf.Rad2Deg);
                    if (bin >= k_HistogramBinCount)
                        bin -= k_HistogramBinCount;
                    if (bin < 0)
                        bin += k_HistogramBinCount;
                    histogram[bin] += mag;
                    ++index;
                }
            }

            // Find the largest orientation value
            var maxVal = histogram[0];
            for (var i = 1; i < k_HistogramBinCount; ++i)
            {
                if (histogram[i] > maxVal)
                    maxVal = histogram[i];
            }

            // Find the peaks that are within 80% of the max peak, and return keypoints for each
            var threshold = maxVal * k_OrientationPeakThresholdFactor;
            for (var i = 0; i < k_HistogramBinCount; ++i)
            {
                var l = i == 0 ? k_HistogramBinCount - 1 : i - 1;
                var r = i == k_HistogramBinCount - 1 ? 0 : i + 1;

                if (histogram[i] <= histogram[l] || histogram[i] < histogram[r] || histogram[i] < threshold)
                    continue;

                // Fit a parabola centered on bin[i] to find the real peak. By using the parabola equation, finding a, b and c, and using the derivative
                // we can find the equation for the peak is:
                var peakBin = i + 0.5f * (histogram[l] - histogram[r]) / (histogram[l] - 2 * histogram[i] + histogram[r]);
                peakBin = peakBin < 0 ? peakBin + k_HistogramBinCount : peakBin >= k_HistogramBinCount ? peakBin - k_HistogramBinCount : peakBin;
                var angle = k_HistogramBinToAngle * peakBin;
                if (angle < k_OrientationAngleEpsilon)
                    angle = 0;

                yield return new KeyPoint()
                {
                    position = keyPoint.position,
                    scaleInfo = keyPoint.scaleInfo,
                    source = keyPoint.source,
                    actualSigma = keyPoint.actualSigma,
                    orientation = angle
                };
            }
        }

        static bool InterpolationOffsetWithinThreshold(Vector3 offset)
        {
            return offset[0] <= k_InterpolationOffsetThreshold && offset[1] <= k_InterpolationOffsetThreshold && offset[2] <= k_InterpolationOffsetThreshold;
        }

        static IEnumerable<KeyPoint> FilterKeyPoints(IEnumerable<KeyPoint> _keyPoints, Octave octave, Func<KeyPoint, Octave, bool> filterFunc)
        {
            foreach (var keyPoint in _keyPoints)
            {
                if (filterFunc(keyPoint, octave))
                    yield return keyPoint;
            }
        }

        static bool CheckContrast(KeyPoint ei, Octave octave, float factor)
        {
            var point = ei.position;
            return MathF.Abs(point.pixelValue.r) >= factor * k_ContrastThreshold;
        }

        static bool WithinUsableScale(int scale)
        {
            return scale is >= 1 and <= k_NumberOfUsableScale;
        }
    }
}
