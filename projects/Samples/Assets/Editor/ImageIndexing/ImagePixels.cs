using System;
using UnityEngine;

namespace UnityEditor.Search
{
    struct PixelPosition
    {
        ImagePixels m_Src;

        public int x;
        public int y;
        public float normalizedX => x / (float)m_Src.width;
        public float normalizedY => y / (float)m_Src.height;

        public ImagePixels source => m_Src;

        public Color pixelValue => m_Src[y, x];

        public PixelPosition(int x, int y, ImagePixels src)
        {
            this.x = x;
            this.y = y;
            this.m_Src = src;
        }
    }

    class ImagePixels
    {
        public int width;
        public int height;
        public Color[] pixels;

        public int size => width * height;

        public Color this[int index]
        {
            get
            {
                return pixels[index];
            }
            set
            {
                pixels[index] = value;
            }
        }

        public Color this[int row, int col]
        {
            get
            {
                return pixels[row * width + col];
            }
            set
            {
                pixels[row * width + col] = value;
            }
        }

        public Color this[float row, float col]
        {
            get
            {
                var discreteRow = (int)Mathf.Floor(row * height);
                var discreteCol = (int)Mathf.Floor(col * width);
                return pixels[discreteRow * width + discreteCol];
            }
            set
            {
                var discreteRow = (int)Mathf.Floor(row * height);
                var discreteCol = (int)Mathf.Floor(col * width);
                pixels[discreteRow * width + discreteCol] = value;
            }
        }

        public ImagePixels(Texture2D texture)
        {
            width = texture.width;
            height = texture.height;
            pixels = TextureUtils.GetPixels(texture);
        }

        public ImagePixels(int width, int height, Color[] pixels)
        {
            this.width = width;
            this.height = height;
            this.pixels = pixels;
        }

        public ImagePixels(int width, int height)
        {
            this.width = width;
            this.height = height;
            this.pixels = new Color[width * height];
        }
    }
}
