using Minecraft_Clone.World;
using Minecraft_Clone.World.Chunks;
using OpenTK.Mathematics;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Minecraft_Clone.World
{
    public enum NoiseLayer
    {
        BASE,
        MOUNTAINBLEND,
        DETAIL,
        TALLGRASS,
        TREE,
    }

    // bundle noise generation parameters
    public struct NoiseParams
    {
        public float scale;
        public int octaves;
        public float lacunarity;
        public float gain;

        public NoiseParams(float scale, int octaves, float lacunarity, float gain)
        {
            this.scale = scale;
            this.octaves = octaves;
            this.lacunarity = lacunarity;
            this.gain = gain;
        }
    }

    // manages world generation...
    public class WorldGenerator
    {
        // World bounds
        public int seaLevel = 64;
        public int minHeight = -128;
        public int maxHeight = 256;

        // Continents (FBM)
        public NoiseParams baseNoiseParams = new NoiseParams(scale: 0.0001f, octaves: 10, lacunarity: 2.4f, gain: 0.5f);
        public float baseHeight = -200f;
        public float baseAmplitude = 1024f;

        // smaller details over terrain
        public NoiseParams detailNoiseParams = new NoiseParams(scale: 0.02f, octaves: 3, lacunarity: 2.1f, gain: 0.3f);
        public float detailAmplitude = 40f;

        // feature generation
        public NoiseParams tallgrassNoiseParams = new NoiseParams(scale: 0.12f, octaves: 3, lacunarity: 2.5f, gain: 0.5f);

        public NoiseParams treeNoiseParams = new NoiseParams(scale: 0.01f, octaves: 2, lacunarity: 1.6f, gain: 0.7f);

        public int seaFloorDepth = 16;
        public float seaFloorBlend = 0.2f;   // sea floor flattening
        public int beachHalfWidth = 3;      // band around sea level for sand

        public int topsoilDepth = 1;
        public int subsoilDepth = 3;

        public NoiseParams mountainBlendNoise = new NoiseParams(scale: 0.02f, octaves: 3, lacunarity: 2.1f, gain: 0.8f);
        public int mountainHeightStart = 128;
        public int snowLineStart = 160;
        public float mountainBoost = 1.1f;

        // Noise
        public readonly NoiseKit noise;
        public int seed = 0;

        // noise caches
        ConcurrentDictionary<NoiseLayer, ConcurrentDictionary<Vector2i, float>> noiseCaches;
        Dictionary<NoiseLayer, NoiseParams> noiseParams = new();

        // height cache
        ConcurrentDictionary<Vector2i, float> heightCache = new();

        float noiseCacheTimer = new(); // after a timer goes to zero, delete the cache from memory

        float noiseCacheLifetime = 30; // 30 frames may pass without noise cache access 

        public WorldGenerator(int seed = 0)
        {
            this.seed = seed;
            noise = new NoiseKit(seed);
            noiseParams.Add(NoiseLayer.BASE, baseNoiseParams);
            noiseParams.Add(NoiseLayer.MOUNTAINBLEND, mountainBlendNoise);
            noiseParams.Add(NoiseLayer.DETAIL, detailNoiseParams);
            noiseParams.Add(NoiseLayer.TALLGRASS, tallgrassNoiseParams);
            noiseParams.Add(NoiseLayer.TREE, treeNoiseParams);
            noiseCaches = new();
        }

        public float GetNoiseAt(NoiseLayer layer, int x, int y)
        {
            noiseCacheTimer = noiseCacheLifetime; // reset timer
            // if the cache layer exists
            if (noiseCaches.TryGetValue(layer, out var cache))
            {
                if (cache.TryGetValue((x, y), out var value))
                    return value;
            }
            else // otherwise add the cache layer
                noiseCaches.TryAdd(layer, new());

            // grab parameters for this layer, calculate the noise once
            noiseParams.TryGetValue(layer, out var p);
            float result = noise.Fbm2D(x * p.scale, y * p.scale, p.octaves, p.lacunarity, p.gain);

            noiseCaches.TryGetValue(layer, out var layerCache); // store the newly calculated val
            layerCache.TryAdd((x, y), result);
            return result;
        }

        // function for calculating terrain height for a given block
        float GetTerrainHeightAt(int worldX, int worldZ)
        {
            if (heightCache.TryGetValue((worldX, worldZ), out float h))
                return h;

            // continental/island
            float baseNoise = GetNoiseAt(NoiseLayer.BASE, worldX, worldZ);
            float height = baseHeight + baseNoise * baseNoise * baseAmplitude;

            // flattening ocean floor (reduce noise)
            if (height < seaLevel)
            {
                float targetFloor = seaLevel - seaFloorDepth;
                float depth = seaLevel - height;
                float maxBlendDepth = seaFloorDepth * 6f + 1f;
                float t = Clamp(depth / maxBlendDepth, 0f, 1f) * seaFloorBlend;
                height = Lerp(height, targetFloor, t);
            }

            float f = GetNoiseAt(NoiseLayer.MOUNTAINBLEND, worldX, worldZ);

            // spikier mountains
            if (height >= mountainHeightStart)
            {
                height *= 1 + (height - mountainHeightStart) / 100 * f * mountainBoost ; 
            }

            float result = Clamp(height, minHeight + 1, maxHeight - 1);
            heightCache.TryAdd((worldX, worldZ), result);
            return result;
        }

        // This is the function to generate terrain
        public BlockType GetBlockAtWorldPos(Vector3i pos)
        {
            int x = pos.X, y = pos.Y, z = pos.Z;

            float hF = GetTerrainHeightAt(x, z);
            int h = (int)MathF.Floor(hF);

            // calculate gradients
            float dx = (GetTerrainHeightAt(x + 1, z) - GetTerrainHeightAt(x - 1, z)) / 3;
            float dz = (GetTerrainHeightAt(x, z + 1) - GetTerrainHeightAt(x, z - 1)) / 3;

            float absdx = MathF.Abs(dx);
            float absdz = MathF.Abs(dz);

            float slope = MathF.Sqrt(absdx * absdx + absdz * absdz);

            // Air / water
            if (y > h)
            {
                if (y <= seaLevel) return BlockType.WATER;
                if (y == h + 1 && y > seaLevel + beachHalfWidth + 1)
                {
                    var t = GetNoiseAt(NoiseLayer.TREE, x, z);
                    if (t > 0.9f)
                        return BlockType.LOG;
                    var f = GetNoiseAt(NoiseLayer.TALLGRASS, x, z);
                    if (y < mountainHeightStart && f > 0.5 && f < 0.55)
                        return BlockType.TALLGRASS;
                }
                return BlockType.AIR;
            }

            // beaches
            if (h >= seaLevel - beachHalfWidth && h <= seaLevel + beachHalfWidth)
            {
                if (y >= h - topsoilDepth + 1) return BlockType.SAND;
                if (y >= h - subsoilDepth) return BlockType.SAND;
            }

            if(slope > 0.9)
                return BlockType.STONE;

            // normal surface
            if (y == h)
            {
                // mountain
                if (h >= mountainHeightStart)
                {
                    if (h > seaLevel + 1 && y >= snowLineStart) return BlockType.SNOW;
                    float m = GetNoiseAt(NoiseLayer.MOUNTAINBLEND, x, z);
                    if (m > 1.0f)
                    {
                        if (slope > 1f) return BlockType.SNOW;
                        return BlockType.STONE;
                    }
                }

                if (h > seaLevel + 1) return BlockType.GRASS;

                // underwater
                if (slope < 0.5) return BlockType.SAND;
                else if (slope < 0.6) return BlockType.DIRT;
                return BlockType.STONE;
            }
            // subsoil
            if (y >= h - subsoilDepth && y < mountainHeightStart) return BlockType.DIRT;

            return BlockType.STONE;
        }

        public void Update()
        {
            if (noiseCacheTimer >= 0)
                noiseCacheTimer--;
            if (noiseCacheTimer == 0)
            {
                Console.WriteLine("Cleared noise cache");
                noiseCaches.Clear();
                heightCache.Clear();
            }
        }

        private static float Clamp(float v, float min, float max) => v < min ? min : (v > max ? max : v);

        private static float Lerp(float a, float b, float t) => a + (b - a) * t; // (fixed)
    }

}
