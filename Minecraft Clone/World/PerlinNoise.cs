using System;

namespace Minecraft_Clone.World
{
    public class PerlinNoise
    {
        private int[] permutation;

        // <summary>
        /// Perlin Noise I got chatgpt to write for me
        /// </summary>
        public PerlinNoise(int seed = 0)
        {
            Random rand = new Random(seed);
            permutation = new int[512];
            int[] p = new int[256];

            for (int i = 0; i < 256; i++) p[i] = i;
            for (int i = 0; i < 256; i++)
            {
                int j = rand.Next(256);
                (p[i], p[j]) = (p[j], p[i]);
            }

            for (int i = 0; i < 512; i++)
            {
                permutation[i] = p[i % 256];
            }
        }

        public float Noise(float x, float y)
        {
            int xi = (int)MathF.Floor(x) & 255;
            int yi = (int)MathF.Floor(y) & 255;

            float xf = x - MathF.Floor(x);
            float yf = y - MathF.Floor(y);

            float u = Fade(xf);
            float v = Fade(yf);

            int aa = permutation[permutation[xi] + yi];
            int ab = permutation[permutation[xi] + yi + 1];
            int ba = permutation[permutation[xi + 1] + yi];
            int bb = permutation[permutation[xi + 1] + yi + 1];

            float x1 = Lerp(Grad(aa, xf, yf), Grad(ba, xf - 1, yf), u);
            float x2 = Lerp(Grad(ab, xf, yf - 1), Grad(bb, xf - 1, yf - 1), u);

            return (Lerp(x1, x2, v) + 1) / 2f; // normalize to [0,1]
        }

        private static float Fade(float t) =>
            t * t * t * (t * (t * 6 - 15) + 10);

        private static float Lerp(float a, float b, float t) =>
            a + t * (b - a);

        private static float Grad(int hash, float x, float y)
        {
            int h = hash & 7;
            float u = h < 4 ? x : y;
            float v = h < 4 ? y : x;
            return ((h & 1) == 0 ? u : -u) + ((h & 2) == 0 ? v : -v);
        }
    }
}
