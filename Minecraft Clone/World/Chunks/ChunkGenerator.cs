using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Minecraft_Clone.World.Chunks
{
    public class ChunkGenerator
    {
        private PerlinNoise noise;
        private float noiseScale;
        //bool busy = false;
        //bool IsBusy() => busy;

        public ChunkGenerator(int seed = 420)
        {
            noise = new PerlinNoise(seed);
        }

        public void GenerateChunk(Chunk chunk)
        {
            //Vector3i worldPos = chunk.index * Chunk.SIZE;
            // for now, we'll just make it a solid chunk
            chunk.FillWithBlock(BlockType.STONE);
        }

        /*
        // <summary>
        /// Generate the world about a point with specific size, asynchronously.
        /// </summary>
        public async Task GenerateChunkAsync(
            Vector3i origin,
            Vector3i size,
            int seaLevel,
            int dirtThickness,
            int sandFallof,
            IProgress<float> progress = null,
            CancellationToken token = default
        )
        {
            // Run the CPU-heavy loop on a ThreadPool thread
            await Task.Run(() =>
            {
                int total = 2 * size.X * 2 * size.Y * 2 * size.Z;
                for (int blockX = 0; blockX < Chunk.SIZE; blockX++)
                {
                    for (int blockY = 0; blockY < Chunk.SIZE; blockY++)
                    {
                        for (int blockZ = 0; blockZ < Chunk.SIZE; blockZ++)
                        {
                            Vector3i blockWorldPos = _chunk.ChunkPosition() * Chunk.SIZE + (blockX, blockY, blockZ);
                            int terrainHeight = CalculateHeight(blockWorldPos.X, blockWorldPos.Z, out bool mountain);

                            // depending on the block's y position, the height of the terrain, and sea level, assign the correct block
                            BlockType blockType = BlockType.AIR;

                            bool isSurface = blockWorldPos.Y == terrainHeight;
                            bool isBelowSurface = blockWorldPos.Y < terrainHeight;
                            bool isFarBelowSurface = blockWorldPos.Y < terrainHeight - dirtThickness;
                            bool isUnderwater = terrainHeight < seaLevel && blockWorldPos.Y <= seaLevel && blockWorldPos.Y > terrainHeight;

                            if (isSurface)
                            {
                                if (blockWorldPos.Y > seaLevel)
                                {
                                    blockType = mountain ? BlockType.STONE : BlockType.GRASS;
                                }
                                else if (blockWorldPos.Y == seaLevel)
                                {
                                    blockType = BlockType.SAND;
                                }
                                else if (blockWorldPos.Y > seaLevel - 10)
                                {
                                    int R = (int)(random.NextDouble() * 10);
                                    blockType = seaLevel - blockWorldPos.Y < R ? BlockType.SAND : BlockType.DIRT;
                                }
                                else
                                {
                                    blockType = mountain ? BlockType.STONE : BlockType.DIRT;
                                }
                            }
                            else if (isFarBelowSurface)
                            {
                                blockType = BlockType.STONE;
                            }
                            else if (isBelowSurface)
                            {
                                blockType = mountain ? BlockType.STONE : BlockType.DIRT;
                            }
                            else if (isUnderwater)
                            {
                                blockType = BlockType.WATER;
                            }

                            // Finally set the block if a type was selected
                            if (blockType != BlockType.AIR)
                            {
                                _chunk.SetBlock(blockX, blockY, blockZ, blockType);
                            }
                        }
                    }
                }
            }, token);
        }

        */
        public int CalculateHeight(int x, int z, out bool mountain)
        {
            float height = 5f * (noise.Noise(x * noiseScale / 4, z * noiseScale / 4) - 0.5f);
            float m = height;
            height += 4f * (noise.Noise(x * noiseScale / 3, z * noiseScale / 3) - 0.5f);
            height += 4f * (noise.Noise(x * noiseScale / 5, z * noiseScale / 5) - 0.5f);

            mountain = false;
            if (m > 0.7f)
            {
                height *= height - 0.7f;
                mountain = true;
            }

            height *= Chunk.SIZE;
            height -= 1;
            if (height < 0)
            {
                height = -MathF.Pow(-height, 0.8f);
            }
            return (int)height;
        }

    }
}
