using Minecraft_Clone.World.Chunks;
using OpenTK.Mathematics;
using System.Collections.Concurrent;

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
        public NoiseParams baseNoiseParams = new NoiseParams(scale: 0.0005f, octaves: 8, lacunarity: 2.3f, gain: 0.5f);
        public float baseHeight = -200f;
        public float baseAmplitude = 1024f;

        // feature generation
        public NoiseParams tallgrassNoiseParams = new NoiseParams(scale: 0.12f, octaves: 3, lacunarity: 2.5f, gain: 0.5f);
        float tallgrassThreshold = 0.05f;// grass half-band around 0.5f

        public NoiseParams treeNoiseParams = new NoiseParams(scale: 1.5f, octaves: 2, lacunarity: 2.5f, gain: 0.8f);
        float treeThreshold = 0.75f; // tree noise greater than this value means a tree is placed
        List<Vector3i> treeLocations;

        public int seaFloorDepth = 32;
        public float seaFloorBlend = 0.2f;   // sea floor flattening
        public int beachHalfWidth = 3;      // band around sea level for sand

        public int topsoilDepth = 1;
        public int subsoilDepth = 3;

        public NoiseParams mountainBlendNoise = new NoiseParams(scale: 0.002f, octaves: 3, lacunarity: 2.4f, gain: 0.8f);
        public int mountainHeightStart = 128;
        public int snowLineOffset = 32;
        public float mountainBoost = 10.0f;

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

        private byte[] treeBlocksArray;

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

            controlPoints = new Vector2[]{
                (minHeight, minHeight),
                (s-150, s-180),
                (s-100, s-60),
                (s-40, s-50),
                (s-15, s-5),
                (s, s),
                (s+25, s+10),
                (s+50, s+25),
                (m+50, m-10),
                (m+100, m+90),
                (maxHeight, maxHeight) }; // needs to be in order along x

            treeLocations = new();
        }

        // piecewise function to flatten/exaggerate cliffs and stuff idk
        public float heightRemapper(float h, Vector2[] controlPoints)
        {
            float result = h;

            for (int i = 0; i < controlPoints.Length - 1; i++)
            {
                var point = controlPoints[i];
                var nextPoint = controlPoints[i + 1];

                // find the zone it falls under
                if (h >= point.X && h < nextPoint.X)
                {
                    result = Lerp(point.Y, nextPoint.Y, (h - point.X) / (nextPoint.X - point.X));
                }
            }

            return result;
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

            height += GetNoiseAt(NoiseLayer.MOUNTAINBLEND, worldX, worldZ) * mountainBoost;

            height = heightRemapper(height, controlPoints);

            float result = Clamp(height, minHeight + 1, maxHeight - 1);
            heightCache.TryAdd((worldX, worldZ), result);
            return result;
        }

        // This is the function to generate terrain, as well as mark locations to place trees
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
                    float t = GetNoiseAt(NoiseLayer.TREE, x, z);
                    if (t > treeThreshold && slope < 2f)
                    {
                        Console.WriteLine($"Added a tree at coords: {(x, y, z)}");
                        MarkTreePos((x, y, z));
                    }
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
                // mountain
                if (h >= mountainHeightStart)
                {
                    if (h > seaLevel + 1 && y >= mountainHeightStart + snowLineOffset) return BlockType.SNOW;
                    return BlockType.STONE;
                }

                // normal grass surface
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

        // grows trees
        public ChunkGenerator.CompletedChunkBlocks GrowFlora(ChunkGenerator.CompletedChunkBlocks blocks)
        {
            var worldOffset = blocks.index * Chunk.SIZE;

            for (byte x = 0; x < Chunk.SIZE; x++)
            {
                for (byte y = 1; y < Chunk.SIZE; y++)
                {
                    for (byte z = 0; z < Chunk.SIZE; z++)
                    {
                        var treePos = worldOffset + (x, y, z);

                        bool growableSurface = blocks.GetBlock(x, y - 1, z).Type == BlockType.GRASS;

                        //// determine if there's tallgrass here
                        if (growableSurface)
                        {
                            float f = GetNoiseAt(NoiseLayer.TALLGRASS, worldOffset.X + x, worldOffset.Z + z);
                            if (f > 0.5f - tallgrassThreshold && f < 0.5f + tallgrassThreshold)
                                blocks.SetBlock((x, y, z), BlockType.TALLGRASS);
                        }

                        if (treeLocations.Contains(treePos))
                        {
                            Console.WriteLine($"found a tree at {(treePos)}");
                            treeLocations.Remove(treePos);

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

        public void MarkTreePos(Vector3i position)
        {
            if (!treeLocations.Contains(position))
            {
                //Console.WriteLine($"[WorldGenerator] Added a tree at x-z {position}");
                treeLocations.Add(position);
            }
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

            Console.WriteLine($"trees stored in memory: {treeLocations.Count}");
        }

        private static float Clamp(float v, float min, float max) => v < min ? min : (v > max ? max : v);

        private static float Lerp(float a, float b, float t) => a + (b - a) * t; // (fixed)


    }
}
