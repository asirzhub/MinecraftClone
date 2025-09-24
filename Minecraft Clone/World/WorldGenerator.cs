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
    public enum NoiseLayer {
        BASE,
        DETAIL,
        TALLGRASS,
    }
    
    // bundle noise generation parameters
    public struct NoiseParams {
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
        public NoiseParams baseNoiseParams = new NoiseParams(scale:0.0003f, octaves:5, lacunarity:2.5f, gain:0.7f);
        public float baseHeight = -200f;
        public float baseAmplitude = 510f;

        // smaller details over terrain
        public NoiseParams detailNoiseParams = new NoiseParams(scale: 0.02f, octaves: 3, lacunarity: 2.1f, gain: 0.3f);
        public float detailAmplitude = 40f;

        // feature generation
        public NoiseParams tallgrassNoiseParams = new NoiseParams(scale: 0.12f, octaves: 3, lacunarity: 2.5f, gain: 0.5f);

        public int seaFloorDepth = 6;
        public float seaFloorBlend = 0.75f;   // sea floor flattening
        public int beachHalfWidth = 3;      // band around sea level for sand

        public int topsoilDepth = 1;
        public int subsoilDepth = 3;

        // Noise
        public readonly NoiseKit noise;
        public int seed = 0;

        // noise caches
        ConcurrentDictionary<NoiseLayer, ConcurrentDictionary<Vector2i, float>> noiseCaches;
        Dictionary<NoiseLayer, NoiseParams> noiseParams = new();
        float noiseCacheTimer = new(); // after a timer goes to zero, delete the cache from memory

        float noiseCacheLifetime = 10; // 10 frames may pass without noise cache access 

        public WorldGenerator(int seed = 0)
        {
            this.seed = seed;
            noise = new NoiseKit(seed);
            noiseParams.Add(NoiseLayer.BASE, baseNoiseParams);
            noiseParams.Add(NoiseLayer.DETAIL, detailNoiseParams);
            noiseParams.Add(NoiseLayer.TALLGRASS, tallgrassNoiseParams);
            noiseCaches = new();
        }

        public float GetNoiseAt(NoiseLayer layer, int x, int y)
        {
            noiseCacheTimer = noiseCacheLifetime; // reset timer
            // if the cache layer exists
            if(noiseCaches.TryGetValue(layer, out var cache))
            {
                if(cache.TryGetValue((x,y), out var value))
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
            // continental/island
            float baseNoise = GetNoiseAt(NoiseLayer.BASE, worldX, worldZ);
            float height = baseHeight + baseNoise * baseAmplitude;

            // small details
            float d = GetNoiseAt(NoiseLayer.DETAIL, worldX, worldZ);
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
                if(y == h + 1 && y > seaLevel + beachHalfWidth + 1)
                {
                    var f = GetNoiseAt(NoiseLayer.TALLGRASS, x, z);
                    if (f > 0.4 && f < 0.6)
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

        public void Update()
        {
            if (noiseCacheTimer >= 0)
                noiseCacheTimer--;
            if (noiseCacheTimer == 0)
            {
                Console.WriteLine("Cleared noise cache");
                noiseCaches.Clear();
            }
        }

        private static float Clamp(float v, float min, float max) => v < min ? min : (v > max ? max : v);

        private static float Lerp(float a, float b, float t) => a + (b - a) * t; // (fixed)
    }

}
