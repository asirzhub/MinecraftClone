using Minecraft_Clone.World.Chunks;
using OpenTK.Mathematics;
using System.Collections.Concurrent;
using System.Diagnostics;
using static Minecraft_Clone.Graphics.CubeMesh;

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

    public struct NoiseCacheEntry
    {
        public float value;
        public int framesSinceUse;

        public NoiseCacheEntry(float value, int framesSinceUse) {
            this.value = value;
            this.framesSinceUse = framesSinceUse;
        }
    }

    // manages world generation...
    public class WorldGenerator
    {
        // World bounds
        public int seaLevel = 0;
        public int minHeight = -128;
        public int maxHeight = 256;

        // Continents (FBM)
        public NoiseParams baseNoiseParams = new NoiseParams(scale: 0.001f, octaves: 6, lacunarity: 2.2f, gain: 0.5f);

        // feature generation
        public NoiseParams tallgrassNoiseParams = new NoiseParams(scale: 0.12f, octaves: 3, lacunarity: 2.5f, gain: 0.5f);
        float tallgrassThreshold = 0.05f;// grass half-band around 0.5f

        public NoiseParams treeNoiseParams = new NoiseParams(scale: 1.5f, octaves: 2, lacunarity: 2.5f, gain: 0.8f);
        float treeThreshold = 0.75f; // tree noise greater than this value means a tree is placed

        public int beachHalfWidth = 3;      // band around sea level for sand
        public int topsoilDepth = 1;
        public int subsoilDepth = 3;

        public NoiseParams mountainBlendNoise = new NoiseParams(scale: 0.01f, octaves: 3, lacunarity: 2.5f, gain: 0.5f);
        public int mountainHeightStart = 128;
        public int snowLineOffset = 32;

        // Noise
        public readonly NoiseKit noise;
        public int seed = 0;

        // noise caches
        ConcurrentDictionary<NoiseLayer, ConcurrentDictionary<Vector2i, NoiseCacheEntry>> noiseCaches;
        Dictionary<NoiseLayer, NoiseParams> noiseParams = new();

        float noiseCacheLifetime = 180;

        readonly Vector2[] controlPoints;

        // define a tree based on vertical slices
        // I need to find a better way to do things
        private BlockType[] treeBlocks = {
            BlockType.AIR, BlockType.AIR, BlockType.AIR, BlockType.AIR, BlockType.AIR,
            BlockType.AIR, BlockType.AIR, BlockType.AIR, BlockType.AIR, BlockType.AIR,
            BlockType.AIR, BlockType.AIR, BlockType.LOG, BlockType.AIR, BlockType.AIR,
            BlockType.AIR, BlockType.AIR, BlockType.AIR, BlockType.AIR, BlockType.AIR,
            BlockType.AIR, BlockType.AIR, BlockType.AIR, BlockType.AIR, BlockType.AIR,

            BlockType.AIR, BlockType.AIR, BlockType.AIR, BlockType.AIR, BlockType.AIR,
            BlockType.AIR, BlockType.AIR, BlockType.AIR, BlockType.AIR, BlockType.AIR,
            BlockType.AIR, BlockType.AIR, BlockType.LOG, BlockType.AIR, BlockType.AIR,
            BlockType.AIR, BlockType.AIR, BlockType.AIR, BlockType.AIR, BlockType.AIR,
            BlockType.AIR, BlockType.AIR, BlockType.AIR, BlockType.AIR, BlockType.AIR,

            BlockType.AIR, BlockType.LEAVES, BlockType.LEAVES, BlockType.LEAVES, BlockType.AIR,
            BlockType.LEAVES, BlockType.LEAVES, BlockType.LEAVES, BlockType.LEAVES, BlockType.LEAVES,
            BlockType.LEAVES, BlockType.LEAVES, BlockType.LOG,    BlockType.LEAVES, BlockType.LEAVES,
            BlockType.LEAVES, BlockType.LEAVES, BlockType.LEAVES, BlockType.LEAVES, BlockType.LEAVES,
            BlockType.AIR, BlockType.LEAVES, BlockType.LEAVES, BlockType.LEAVES, BlockType.AIR,

            BlockType.AIR, BlockType.LEAVES, BlockType.LEAVES, BlockType.LEAVES, BlockType.AIR,
            BlockType.LEAVES, BlockType.LEAVES, BlockType.LEAVES, BlockType.LEAVES, BlockType.LEAVES,
            BlockType.LEAVES, BlockType.LEAVES, BlockType.LOG,    BlockType.LEAVES, BlockType.LEAVES,
            BlockType.LEAVES, BlockType.LEAVES, BlockType.LEAVES, BlockType.LEAVES, BlockType.LEAVES,
            BlockType.AIR, BlockType.LEAVES, BlockType.LEAVES, BlockType.LEAVES, BlockType.AIR,

            BlockType.AIR, BlockType.AIR, BlockType.LEAVES, BlockType.AIR, BlockType.AIR,
            BlockType.AIR, BlockType.LEAVES, BlockType.LEAVES, BlockType.LEAVES, BlockType.AIR,
            BlockType.LEAVES, BlockType.LEAVES, BlockType.LEAVES,    BlockType.LEAVES, BlockType.LEAVES,
            BlockType.AIR, BlockType.LEAVES, BlockType.LEAVES, BlockType.LEAVES, BlockType.AIR,
            BlockType.AIR, BlockType.AIR, BlockType.LEAVES, BlockType.AIR, BlockType.AIR,

            BlockType.AIR, BlockType.AIR, BlockType.AIR, BlockType.AIR, BlockType.AIR,
            BlockType.AIR, BlockType.AIR, BlockType.LEAVES, BlockType.AIR, BlockType.AIR,
            BlockType.AIR, BlockType.LEAVES, BlockType.LEAVES,    BlockType.LEAVES, BlockType.AIR,
            BlockType.AIR, BlockType.AIR, BlockType.LEAVES, BlockType.AIR, BlockType.AIR,
            BlockType.AIR, BlockType.AIR, BlockType.AIR, BlockType.AIR, BlockType.AIR,
        };

        public WorldGenerator(int seed = 0)
        {
            this.seed = seed;
            noise = new NoiseKit(seed);
            noiseParams.Add(NoiseLayer.BASE, baseNoiseParams);
            noiseParams.Add(NoiseLayer.MOUNTAINBLEND, mountainBlendNoise);
            noiseParams.Add(NoiseLayer.TALLGRASS, tallgrassNoiseParams);
            noiseParams.Add(NoiseLayer.TREE, treeNoiseParams);
            noiseCaches = new();

            float s = seaLevel;
            float m = mountainHeightStart;
        }

        public float GetNoiseAt(NoiseLayer layer, int x, int z)
        {
            //if (!noiseCaches.TryGetValue(layer, out var cache))
            //{
            //    cache = new ConcurrentDictionary<Vector2i, NoiseCacheEntry>();
            //    noiseCaches.TryAdd(layer, cache);
            //}

            var key = new Vector2i(x, z);
            float result = 0;

            //// existing entry
            //if (cache.TryGetValue(key, out var entry))
            //{
            //    cache[key] = new NoiseCacheEntry(entry.value, frameCount);
            //    result = entry.value;
            //}
            //else // new entry
            //{
                noiseParams.TryGetValue(layer, out var prms);
                result = noise.Fbm2D(x * prms.scale, z * prms.scale, prms.octaves, prms.lacunarity, prms.gain);
                //cache[key] = new NoiseCacheEntry(result, frameCount);
            //}

            return result;                
        }

        // This is the function to generate terrain, as well as mark locations to place trees
        public BlockType GetBlockAtWorldPos(Vector3i pos)
        {
            int x = pos.X, y = pos.Y, z = pos.Z;

            if (y < minHeight || y > maxHeight)
                return BlockType.AIR;

            float continent = GetNoiseAt(NoiseLayer.BASE, x, z);

            if (continent > 0.5f) continent = 2.0f - 2.0f * continent;
            else continent *= 2.0f;

            continent *= continent * continent ;

            int height = (int)(continent * (maxHeight - minHeight) + minHeight);


            if (y < height)
            {
                float fbm = noise.Fbm3D(x, y, z);
                if (fbm < 0.1f) return BlockType.AIR;
                return BlockType.STONE;
            }

            if (y < seaLevel)
            {
                return BlockType.WATER;
            }

            return BlockType.AIR;
        }

        // grows trees
        public ChunkGenerator.CompletedChunkBlocks GrowFlora(ChunkGenerator.CompletedChunkBlocks blocks, ChunkManager manager)
        {
            var worldOffset = blocks.index * Chunk.SIZE;

            for (byte x = 0; x < Chunk.SIZE; x++)
            {
                for (byte y = 0; y < Chunk.SIZE; y++)
                {
                    for (byte z = 0; z < Chunk.SIZE; z++)
                    {
                        var treePos = worldOffset + (x, y, z);

                        bool growableSurface = manager.TryGetBlockAtWorldPosition(worldOffset + (x, y - 1, z), out var result) && result.Type == BlockType.GRASS;

                        // determine if there's tallgrass here
                        if (growableSurface)
                        {
                            float f = GetNoiseAt(NoiseLayer.TALLGRASS, worldOffset.X + x, worldOffset.Z + z);
                            if (f > 0.5f - tallgrassThreshold && f < 0.5f + tallgrassThreshold)
                            {
                                blocks.SetBlock((x, y, z), BlockType.TALLGRASS);
                                continue;
                            }
                        }

                        float t = GetNoiseAt(NoiseLayer.TREE, worldOffset.X + x, worldOffset.Z + z);

                        if (t > treeThreshold)
                        {
                            if (growableSurface)
                            {
                                for (int tx = 0; tx < 5; tx++)
                                {
                                    for (int ty = 0; ty < 6; ty++)
                                    {
                                        for (int tz = 0; tz < 5; tz++)
                                        {
                                            int blockidx = (ty * 5 + tz) * 5 + tx;
                                            var blockType = treeBlocks[blockidx];
                                            if (blockType != BlockType.AIR) blocks.SetBlock((x + tx - 2, y + ty, z + tz - 2), blockType);
                                        }
                                    }
                                }
                                blocks.SetBlock((x, y - 1, z), BlockType.DIRT);
                            }
                        }

                    }
                }
            }

            return blocks;
        }

        public Vector3i WorldPosToChunkIndex(Vector3i worldIndex, int chunkSize = Chunk.SIZE)
        {
            int chunkX = (int)Math.Floor(worldIndex.X / (double)chunkSize);
            int chunkY = (int)Math.Floor(worldIndex.Y / (double)chunkSize);
            int chunkZ = (int)Math.Floor(worldIndex.Z / (double)chunkSize);

            return (chunkX, chunkY, chunkZ);
        }

        int frameCount = -10;

        public async void Update(int frameCount)
        {
            if (frameCount - this.frameCount < 15) return; // avoid double-calls

            Console.WriteLine($"Searching for expired noise entries at frame: {frameCount}");
            int removed = 0;
            int total = 0;

            // remove expired noise cache entries
            this.frameCount = frameCount;
            await Task.Run(() =>
            {

                foreach (var (layer, dict) in noiseCaches)
                {
                    foreach (var kv in dict) // kv is KeyValuePair<Vector2i, NoiseCacheEntry>
                    {
                        total++;
                        if (frameCount - kv.Value.framesSinceUse > noiseCacheLifetime)
                        {
                            if(dict.TryRemove(kv.Key, out _)) removed++;
                        }
                    }
                }
                Console.WriteLine($"Removed {removed} entries - {100 * (float)removed / total}%");
                GC.Collect();
            });

        }

        private static float Lerp(float a, float b, float t) => a + (b - a) * t;

        float smoothstep(float edge0, float edge1, float x)
        {
            x = clamp((x - edge0) / (edge1 - edge0));
            return x * x * (3.0f - 2.0f * x);
        }

        float clamp(float x, float lowerlimit = 0.0f, float upperlimit = 1.0f)
        {
            if (x < lowerlimit) return lowerlimit;
            if (x > upperlimit) return upperlimit;
            return x;
        }
    }
}
