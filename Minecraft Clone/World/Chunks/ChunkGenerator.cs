using OpenTK.Mathematics;
using System;
using System.Diagnostics;

namespace Minecraft_Clone.World.Chunks
{
    public class ChunkGenerator
    {
        private PerlinNoise noise;
        private float noiseScale;
        public float surfaceThreshold = 0.5f;

        public ChunkGenerator(int seed = 420, float noiseScale = 0.01f)
        {
            noise = new PerlinNoise(seed);
            this.noiseScale = noiseScale;
        }

        Stopwatch watch = new Stopwatch();
        /**
        public async Task GenerateChunkAsync(Chunk chunk, Vector3i chunkIndex, ChunkWorld world, CancellationToken token)
        {
            await Task.Run(() =>
            {
                token.ThrowIfCancellationRequested();

                watch.Start();
                int totalBlocks = 0;

                int seaLevel = world.seaLevel;
                int dirtThickness = world.dirtThickness;
                int sandFalloff = world.sandFalloff;

                // for each block in the 16×16×16 volume...
                for (int x = 0; x < Chunk.SIZE; x++)
                {
                    for (int y = Chunk.SIZE - 1; y >= 0; y--)
                    {
                        for (int z = 0; z < Chunk.SIZE; z++)
                        {
                            // compute world-space coordinate of this block
                            int worldX = chunkIndex.X * Chunk.SIZE + x;
                            int worldY = chunkIndex.Y * Chunk.SIZE + y;
                            int worldZ = chunkIndex.Z * Chunk.SIZE + z;
                            BlockType type = BlockType.AIR;

                            if (worldY < seaLevel)
                            {
                                type = BlockType.AIR;
                            }

                            var landBias = 3 * Chunk.SIZE * (noise.Noise((float)worldX * noiseScale, (float)worldZ * noiseScale)-0.5);

                            if (worldY <= seaLevel)
                                type = BlockType.WATER;

                            if (worldY < landBias && worldY > -64)
                                type = BlockType.STONE;

                            chunk.SetBlock(x, y, z, type);
                            if (type != BlockType.AIR) totalBlocks++;
                        }
                    }
                }

                watch.Stop();
                Console.WriteLine($"Generating terrain took {watch.ElapsedMilliseconds}ms for chunk: {chunkIndex} with total {totalBlocks} ({100 * totalBlocks / 32768}% full) blocks.");
                watch.Reset();
            });
        }*/
    }
}
