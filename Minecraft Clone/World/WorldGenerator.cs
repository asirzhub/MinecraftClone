using Minecraft_Clone.World;
using Minecraft_Clone.World.Chunks;
using Minecraft_Clone.World.SurfaceFeatures;
using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using static System.Formats.Asn1.AsnWriter;

namespace Minecraft_Clone.World
{
    // manages world generation...
    public class WorldGenerator
    {
        // World bounds
        public int seaLevel = 64;
        public int minHeight = -128;
        public int maxHeight = 256;

        // Continents (FBM)
        public float baseScale = 0.0005f;
        public int baseOctaves = 4;         
        public float baseLacunarity = 2.5f;
        public float baseGain = 0.7f;
        public float baseHeight = -200f;
        public float baseAmplitude = 510f;

        // smaller details over terrain
        public float detailScale = 0.05f;
        public int detailOctaves = 2;       
        public float detailAmplitude = 15f;

        public int seaFloorDepth = 6;
        public float seaFloorBlend = 0.75f;   // sea floor flattening
        public int beachHalfWidth = 3;      // band around sea level for sand

        public int topsoilDepth = 1;
        public int subsoilDepth = 3;

        // Noise
        public readonly NoiseKit noise;
        public int seed = 0;

        public WorldGenerator(int seed = 0)
        {
            this.seed = seed;
            noise = new NoiseKit(seed);
        }

        // function for calculating terrain height for a given block
        float GetTerrainHeightAt(int worldX, int worldZ)
        {
            // continental/island
            float baseNoise = noise.Fbm2D(worldX * baseScale, worldZ * baseScale,
                                          baseOctaves, baseLacunarity, baseGain);
            float height = baseHeight + baseNoise * baseAmplitude;

            // small details
            float d = noise.Fbm2D(worldX * detailScale, worldZ * detailScale,
                                  detailOctaves, 2.0f, 0.5f);
            height += d * detailAmplitude;

            // flattening ocean floor (reduce noise)
            if (height < seaLevel)
            {
                float targetFloor = seaLevel - seaFloorDepth;
                float depth = seaLevel - height;
                float maxBlendDepth = seaFloorDepth * 6f + 1f;  
                float t = Clamp(depth / maxBlendDepth, 0f, 1f) * seaFloorBlend;
                height = Lerp(height, targetFloor, t);
            }

            return Clamp(height, minHeight + 1, maxHeight - 1);
        }

        // This is the function to generate terrain
        public BlockType GetBlockAtWorldPos(Vector3i pos)
        {
            int x = pos.X, y = pos.Y, z = pos.Z;

            float hF = GetTerrainHeightAt(x, z);
            int h = (int)MathF.Floor(hF);

            // Air / water
            if (y > h)
            {
                if (y <= seaLevel) return BlockType.WATER;
                return BlockType.AIR;
            }

            // beaches
            if (h >= seaLevel - beachHalfWidth && h <= seaLevel + beachHalfWidth)
            {
                if (y >= h - topsoilDepth + 1) return BlockType.SAND;
                if (y >= h - subsoilDepth) return BlockType.SAND;
            }

            // normal surface
            if (y == h)
            {
                if (h > seaLevel + 1) return BlockType.GRASS;
                return BlockType.STONE;
            }

            // subsoil
            if (y >= h - subsoilDepth) return BlockType.DIRT;

            return BlockType.STONE;
        }

        private static float Clamp(float v, float min, float max) => v < min ? min : (v > max ? max : v);

        private static float Lerp(float a, float b, float t) => a + (b - a) * t;
    }

}
