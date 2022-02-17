using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityEditor.Search
{
    interface IImageFilter
    {
        ImagePixels Apply(ImagePixels source);
    }

    class SobelXFilter : IImageFilter, IEditorImageFilter
    {
        public ImagePixels Apply(ImagePixels source)
        {
            var edge = Filtering.Convolve(source, SobelFilter.SobelX);
            var stretchedImage = ImageUtils.StretchImage(edge, 0f, 1f);
            return stretchedImage;
        }

        public void PopulateParameters(VisualElement root, Action onFilterChanged)
        {}

        [EditorFilter("SobelX")]
        public static IEditorImageFilter Create()
        {
            return new SobelXFilter();
        }
    }

    class SobelYFilter : IImageFilter, IEditorImageFilter
    {
        public ImagePixels Apply(ImagePixels source)
        {
            var edge = Filtering.Convolve(source, SobelFilter.SobelY);
            var stretchedImage = ImageUtils.StretchImage(edge, 0f, 1f);
            return stretchedImage;
        }

        public void PopulateParameters(VisualElement root, Action onFilterChanged)
        {}

        [EditorFilter("SobelY")]
        public static IEditorImageFilter Create()
        {
            return new SobelYFilter();
        }
    }

    class SobelFilter : IImageFilter, IEditorImageFilter
    {
        public static readonly Kernel SobelX = new Kernel(3, 3, new double[] { 1, 0, -1, 2, 0, -2, 1, 0, -1 });
        public static readonly Kernel SobelY = new Kernel(3, 3, new double[] { 1, 2, 1, 0, 0, 0, -1, -2, -1 });

        public Color[] gradients { get; private set; }

        public float threshold;

        public SobelFilter(float threshold)
        {
            this.threshold = threshold;
        }

        public ImagePixels Apply(ImagePixels source)
        {
            var edgeX = Filtering.Convolve(source, SobelX);
            var edgeY = Filtering.Convolve(source, SobelY);

            var magnitudePixels = new Color[source.height * source.width];
            gradients = new Color[source.height * source.width];
            var rangeSize = ThreadUtils.GetBatchSizeByCore(source.height);
            Parallel.ForEach(Partitioner.Create(0, source.height, rangeSize), range =>
            {
                for (var i = range.Item1; i < range.Item2; ++i)
                {
                    for (var j = 0; j < source.width; ++j)
                    {
                        var index = i * source.width + j;
                        var colorX = edgeX.pixels[index];
                        var colorY = edgeY.pixels[index];
                        var edgeOutput = new Color();
                        var gradientOutput = new Color();
                        for (var k = 0; k < 3; ++k)
                        {
                            var mag = MathUtils.Clamp(Mathf.Sqrt((colorX[k] * colorX[k]) + (colorY[k] * colorY[k])), 0f, 1.0f);
                            edgeOutput[k] = mag >= threshold ? 1f : 0f;
                            gradientOutput[k] = Mathf.Atan2(colorY[k], colorX[k]);
                        }

                        magnitudePixels[index] = edgeOutput;
                        gradients[index] = gradientOutput;
                    }
                }
            });

            return new ImagePixels(source.width, source.height, magnitudePixels);
        }

        public void PopulateParameters(VisualElement root, Action onFilterChanged)
        {
            var floatField = new Slider("Threshold", 0, 1);
            floatField.style.flexGrow = 1;
            floatField.value = threshold;
            floatField.RegisterValueChangedCallback(evt =>
            {
                threshold = evt.newValue;
                onFilterChanged();
            });
            floatField.showInputField = true;
            root.Add(floatField);
        }

        [EditorFilter("Sobel")]
        public static IEditorImageFilter Create()
        {
            return new SobelFilter(0.25f);
        }
    }

    class GaussianFilter : IImageFilter, IEditorImageFilter
    {
        Kernel m_KernelX;
        Kernel m_KernelY;

        int m_Size;
        public int size
        {
            get
            {
                return m_Size;
            }
            set
            {
                m_Size = value;
                RebuildKernels(size, sigma);
            }
        }

        double m_Sigma;
        public double sigma
        {
            get
            {
                return m_Sigma;
            }
            set
            {
                m_Sigma = value;
                RebuildKernels(size, sigma);
            }
        }

        public GaussianFilter(int size, double sigma)
        {
            RebuildKernels(size, sigma);
        }

        public ImagePixels Apply(ImagePixels source)
        {
            var resultX = Filtering.Convolve(source, m_KernelX);
            return Filtering.Convolve(resultX, m_KernelY);
        }

        void RebuildKernels(int size, double sigma)
        {
            if (!MathUtils.IsOdd(size))
                throw new ArgumentException("Kernel size must be odd.", nameof(size));

            var kernelValues = new double[size];
            var halfSize = size / 2;
            var sigmaSquare = sigma * sigma;
            var expoScale = 1 / (2 * 3.14159 * sigmaSquare);
            for (var halfX = -halfSize; halfX <= halfSize; ++halfX)
            {
                var value = expoScale * Math.Exp(-(halfX * halfX) / (2 * sigmaSquare));
                kernelValues[halfX + halfSize] = value;
            }

            m_KernelX = new Kernel(size, 1, kernelValues);
            m_KernelY = new Kernel(1, size, kernelValues);
        }

        public void PopulateParameters(VisualElement root, Action onFilterChanged)
        {
            // var intSlider = new SliderInt("Size", 1, 11, SliderDirection.Horizontal, 2F);
            // intSlider.style.flexGrow = 1;
            // intSlider.value = size;
            // intSlider.RegisterValueChangedCallback(evt =>
            // {
            //     if (evt.newValue % 2 == 1)
            //     {
            //         size = evt.newValue;
            //         onFilterChanged();
            //     }
            // });
            // intSlider.showInputField = true;
            // root.Add(intSlider);

            var floatField = new Slider("Sigma", 0.001f, 25);
            floatField.style.flexGrow = 1;
            floatField.value = (float)sigma;
            floatField.RegisterValueChangedCallback(evt =>
            {
                m_Size = GetSizeFromSigma(evt.newValue);
                sigma = evt.newValue;
                onFilterChanged();
            });
            floatField.showInputField = true;
            root.Add(floatField);
        }

        [EditorFilter("Gaussian")]
        public static IEditorImageFilter Create()
        {
            return new GaussianFilter(3, 1.5);
        }

        public static int GetSizeFromSigma(double sigma)
        {
            var size = Mathf.FloorToInt(3 * (float)sigma);
            if (size % 2 == 0)
                ++size;
            return size;
        }
    }

    class DifferenceOfGaussian : IImageFilter, IEditorImageFilter
    {
        GaussianFilter m_SmallFilter;
        GaussianFilter m_LargeFilter;

        public int smallSize
        {
            get => m_SmallFilter.size;
            set => m_SmallFilter.size = value;
        }

        public int largeSize
        {
            get => m_LargeFilter.size;
            set => m_LargeFilter.size = value;
        }

        public double sigma
        {
            get => m_SmallFilter.sigma;
            set
            {
                m_SmallFilter.sigma = value;
                m_LargeFilter.sigma = value;
            }
        }

        public bool stretchImageForViewing { get; private set; }

        public DifferenceOfGaussian(int sizeSmall, int sizeLarge, double sigma, bool stretchImageForViewing = false)
        {
            m_SmallFilter = new GaussianFilter(sizeSmall, sigma);
            m_LargeFilter = new GaussianFilter(sizeLarge, sigma);
            this.stretchImageForViewing = stretchImageForViewing;
        }

        public ImagePixels Apply(ImagePixels source)
        {
            var sourceA = m_SmallFilter.Apply(source);
            var sourceB = m_LargeFilter.Apply(source);

            var sub = Filtering.Subtract(sourceA, sourceB);
            if (stretchImageForViewing)
                return ImageUtils.StretchImage(sub, 0.0f, 1.0f);
            return sub;
        }

        public void PopulateParameters(VisualElement root, Action onFilterChanged)
        {
            // var intSlider = new SliderInt("Size Small", 1, 11, SliderDirection.Horizontal, 2F);
            // intSlider.style.flexGrow = 1;
            // intSlider.value = smallSize;
            // intSlider.RegisterValueChangedCallback(evt =>
            // {
            //     if (evt.newValue % 2 == 1)
            //     {
            //         smallSize = evt.newValue;
            //         onFilterChanged();
            //     }
            // });
            // intSlider.showInputField = true;
            // root.Add(intSlider);
            //
            // var largeIntSlider = new SliderInt("Size Small", 3, 17, SliderDirection.Horizontal, 2F);
            // largeIntSlider.style.flexGrow = 1;
            // largeIntSlider.value = largeSize;
            // largeIntSlider.RegisterValueChangedCallback(evt =>
            // {
            //     if (evt.newValue % 2 == 1)
            //     {
            //         largeSize = evt.newValue;
            //         onFilterChanged();
            //     }
            // });
            // largeIntSlider.showInputField = true;
            // root.Add(largeIntSlider);

            var floatField = new Slider("Sigma", 0.001f, 25);
            floatField.style.flexGrow = 1;
            floatField.value = (float)sigma;
            floatField.RegisterValueChangedCallback(evt =>
            {
                smallSize = GaussianFilter.GetSizeFromSigma(evt.newValue);
                largeSize = smallSize + 2;
                sigma = evt.newValue;
                onFilterChanged();
            });
            floatField.showInputField = true;
            root.Add(floatField);
        }

        [EditorFilter("DoG")]
        public static IEditorImageFilter Create()
        {
            return new DifferenceOfGaussian(3, 5, 0.6, true);
        }
    }
}
