using System;
using UnityEngine;

namespace UnityEditor.Search
{
    static class MathUtils
    {
        public static readonly double k_Sqrt2 = Math.Sqrt(2.0);
        public static readonly double k_Sqrt3 = Math.Sqrt(3.0);
        private static System.Random s_Rand = new System.Random();

        public static Vector3Int[] Grad3 { get; } =
        {
            new Vector3Int(1, 1, 0), new Vector3Int(-1, 1, 0), new Vector3Int(1, -1, 0), new Vector3Int(-1, -1, 0),
            new Vector3Int(1, 0, 1), new Vector3Int(-1, 0, 1), new Vector3Int(1, 0, -1), new Vector3Int(-1, 0, -1),
            new Vector3Int(0, 1, 1), new Vector3Int(0, -1, 1), new Vector3Int(0, 1, -1), new Vector3Int(0, -1, -1)
        };

        public static Vector4[] Grad4 { get; } =
        {
            new Vector4(0, 1, 1, 1), new Vector4(0, 1, 1, -1), new Vector4(0, 1, -1, 1), new Vector4(0, 1, -1, -1),
            new Vector4(0, -1, 1, 1), new Vector4(0, -1, 1, -1), new Vector4(0, -1, -1, 1), new Vector4(0, -1, -1, -1),
            new Vector4(1, 0, 1, 1), new Vector4(1, 0, 1, -1), new Vector4(1, 0, -1, 1), new Vector4(1, 0, -1, -1),
            new Vector4(-1, 0, 1, 1), new Vector4(-1, 0, 1, -1), new Vector4(-1, 0, -1, 1), new Vector4(-1, 0, -1, -1),
            new Vector4(1, 1, 0, 1), new Vector4(1, 1, 0, -1), new Vector4(1, -1, 0, 1), new Vector4(1, -1, 0, -1),
            new Vector4(-1, 1, 0, 1), new Vector4(-1, 1, 0, -1), new Vector4(-1, -1, 0, 1), new Vector4(-1, -1, 0, -1),
            new Vector4(1, 1, 1, 0), new Vector4(1, 1, -1, 0), new Vector4(1, -1, 1, 0), new Vector4(1, -1, -1, 0),
            new Vector4(-1, 1, 1, 0), new Vector4(-1, 1, -1, 0), new Vector4(-1, -1, 1, 0), new Vector4(-1, -1, -1, 0)
        };

        public static int[] P { get; } =
        {
            151, 160, 137, 91, 90, 15,
            131, 13, 201, 95, 96, 53, 194, 233, 7, 225, 140, 36, 103, 30, 69, 142, 8, 99, 37, 240, 21, 10, 23,
            190, 6, 148, 247, 120, 234, 75, 0, 26, 197, 62, 94, 252, 219, 203, 117, 35, 11, 32, 57, 177, 33,
            88, 237, 149, 56, 87, 174, 20, 125, 136, 171, 168, 68, 175, 74, 165, 71, 134, 139, 48, 27, 166,
            77, 146, 158, 231, 83, 111, 229, 122, 60, 211, 133, 230, 220, 105, 92, 41, 55, 46, 245, 40, 244,
            102, 143, 54, 65, 25, 63, 161, 1, 216, 80, 73, 209, 76, 132, 187, 208, 89, 18, 169, 200, 196,
            135, 130, 116, 188, 159, 86, 164, 100, 109, 198, 173, 186, 3, 64, 52, 217, 226, 250, 124, 123,
            5, 202, 38, 147, 118, 126, 255, 82, 85, 212, 207, 206, 59, 227, 47, 16, 58, 17, 182, 189, 28, 42,
            223, 183, 170, 213, 119, 248, 152, 2, 44, 154, 163, 70, 221, 153, 101, 155, 167, 43, 172, 9,
            129, 22, 39, 253, 19, 98, 108, 110, 79, 113, 224, 232, 178, 185, 112, 104, 218, 246, 97, 228,
            251, 34, 242, 193, 238, 210, 144, 12, 191, 179, 162, 241, 81, 51, 145, 235, 249, 14, 239, 107,
            49, 192, 214, 31, 181, 199, 106, 157, 184, 84, 204, 176, 115, 121, 50, 45, 127, 4, 150, 254,
            138, 236, 205, 93, 222, 114, 67, 29, 24, 72, 243, 141, 128, 195, 78, 66, 215, 61, 156, 180
        };

        public static int FastFloor(double x)
        {
            return x > 0 ? (int)x : (int)x - 1;
        }

        public static double Dot(Vector3Int g, double x, double y, double z)
        {
            return g[0] * x + g[1] * y + g[2] * z;
        }

        public static double Dot(Vector3 g, double x, double y, double z)
        {
            return g[0] * x + g[1] * y + g[2] * z;
        }

        // Used for 2d
        public static double Dot(Vector3Int g, double x, double y)
        {
            return g[0] * x + g[1] * y;
        }

        public static double Dot(Vector3 g, double x, double y)
        {
            return g[0] * x + g[1] * y;
        }

        public static double Dot(Vector4 g, double x, double y, double z, double w)
        {
            return g[0] * x + g[1] * y + g[2] * z + g[3] * w;
        }

        public static double Mix(double a, double b, double t)
        {
            return (1.0 - t) * a + t * b;
        }

        public static double Fade(double t)
        {
            return t * t * t * (t * (t * 6.0 - 15.0) + 10.0);
        }

        public static Vector3 RandomVector3()
        {
            var vector = new Vector3(s_Rand.Next(), s_Rand.Next(), s_Rand.Next()).normalized;
            return vector;
        }

        public static bool IsOdd(int number)
        {
            return (number % 2) != 0;
        }

        public static double Clamp(double v, double min, double max)
        {
            if (v < min)
                return min;
            if (v > max)
                return max;
            return v;
        }

        public static float Clamp(float v, float min, float max)
        {
            if (v < min)
                return min;
            if (v > max)
                return max;
            return v;
        }

        public static double Sum(double[] values)
        {
            var sum = 0.0;
            foreach (var value in values)
            {
                sum += value;
            }

            return sum;
        }

        public static float Sum(float[] values)
        {
            var sum = 0.0f;
            foreach (var value in values)
            {
                sum += value;
            }

            return sum;
        }

        public static float SumSqrt(float[] values)
        {
            var sum = 0.0f;
            foreach (var value in values)
            {
                sum += Mathf.Sqrt(value);
            }

            return sum;
        }

        public static double SafeDivide(double a, double b, double defaultValue = 1.0)
        {
            if (b == 0)
                return defaultValue;
            return a / b;
        }

        public static float RadToDeg(float rad)
        {
            return Mathf.Rad2Deg * rad;
        }

        public static double Distance(double[] vecA, double[] vecB)
        {
            var sum = 0.0;
            for (var i = 0; i < vecA.Length; ++i)
            {
                var diff = vecA[i] - vecB[i];
                sum += diff * diff;
            }

            return Math.Sqrt(sum);
        }

        public static double NormalizedDistance(double[] vecA, double[] vecB)
        {
            return Distance(vecA, vecB) / k_Sqrt3;
        }
    }
}
