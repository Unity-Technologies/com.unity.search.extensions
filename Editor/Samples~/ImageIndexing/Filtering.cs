using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using UnityEngine;

namespace UnityEditor.Search
{
    class Kernel
    {
        public int sizeX;
        public int sizeY;

        public double[] values;

        public double factor { get; }

        public double this[int y, int x] => values[y * sizeX + x];

        public Kernel(int sizeX, int sizeY, double[] values)
            : this(sizeX, sizeY, MathUtils.SafeDivide(1.0, MathUtils.Sum(values)), values)
        {}

        public Kernel(int sizeX, int sizeY, double factor, double[] values)
        {
            this.sizeX = sizeX;
            this.sizeY = sizeY;
            this.values = values;
            this.factor = factor;
        }
    }

    static class Filtering
    {
        public static ImagePixels Convolve(ImagePixels texture, Kernel kernel)
        {
            var width = texture.width;
            var height = texture.height;
            var outputPixels = new Color[width * height];

            var pixels = texture.pixels;

            var halfXOffset = kernel.sizeX / 2;
            var halfYOffset = kernel.sizeY / 2;

            var rangeSize = ThreadUtils.GetBatchSizeByCore(height);
            var result = Parallel.ForEach(Partitioner.Create(0, height, rangeSize), range =>
            {
                for (var i = range.Item1; i < range.Item2; ++i)
                {
                    for (var j = 0; j < width; ++j)
                    {
                        var currentPixelIndex = i * width + j;
                        var currentPixel = pixels[currentPixelIndex];
                        var outputPixel = new Color();
                        for (var m = -halfYOffset; m <= halfYOffset; ++m)
                        {
                            var offsetY = i + m;
                            if (offsetY < 0 || offsetY >= height)
                                continue;
                            for (var n = -halfXOffset; n <= halfXOffset; ++n)
                            {
                                var offsetX = j + n;
                                if (offsetX < 0 || offsetX >= width)
                                    continue;

                                var offsetPixel = pixels[offsetY * width + offsetX];

                                // In a convolution, the signal is inverted
                                var kernelValue = kernel[-m + halfYOffset, -n + halfXOffset];

                                outputPixel += (float)(kernel.factor * kernelValue) * offsetPixel[3] * offsetPixel;
                            }
                        }

                        // Set the same alpha as the input
                        outputPixel[3] = currentPixel[3];
                        outputPixels[i * width + j] = outputPixel;
                    }
                }
            });

            if (!result.IsCompleted)
                Debug.LogError("Filtering did not complete successfully.");

            var outputTexture = new ImagePixels(width, height, outputPixels);
            return outputTexture;
        }

        public static ImagePixels Subtract(ImagePixels sourceA, ImagePixels sourceB)
        {
            if (sourceA.height != sourceB.height || sourceA.width != sourceB.width)
                throw new ArgumentException("Images don't have the same size");

            var width = sourceA.width;
            var height = sourceA.height;
            var outputPixels = new Color[width * height];

            var pixelsA = sourceA.pixels;
            var pixelsB = sourceB.pixels;

            var batchSize = ThreadUtils.GetBatchSizeByCore(height);
            Parallel.ForEach(Partitioner.Create(0, height, batchSize), range =>
            {
                for (var i = range.Item1; i < range.Item2; ++i)
                {
                    for (var j = 0; j < width; ++j)
                    {
                        var index = i * width + j;
                        outputPixels[index] = pixelsA[index] - pixelsB[index];
                    }
                }
            });

            return new ImagePixels(width, height, outputPixels);
        }
    }
}
