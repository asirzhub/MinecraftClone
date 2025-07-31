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

        public ChunkGenerator(int seed = 420, float noiseScale = 0.01f)
        {
            noise = new PerlinNoise(seed);
            this.noiseScale = noiseScale;
        }

        /// <summary>
        /// Generate terrain in this chunk based on perlin noise + world rules.
        /// </summary>
        public void GenerateChunk(Chunk chunk, Vector3i chunkIndex, ChunkWorld world)
        {
            int seaLevel = world.seaLevel;
            int dirtThickness = world.dirtThickness;
            int sandFalloff = world.sandFalloff;

            // for each block in the 16×16×16 volume...
            for (int x = 0; x < Chunk.SIZE; x++)
            {
                for (int y = 0; y < Chunk.SIZE; y++)
                {
                    for (int z = 0; z < Chunk.SIZE; z++)
                    {
                        // compute world-space coordinate of this block
                        int worldX = chunkIndex.X * Chunk.SIZE + x;
                        int worldY = chunkIndex.Y * Chunk.SIZE + y;
                        int worldZ = chunkIndex.Z * Chunk.SIZE + z;

                        // how high is the terrain here?
                        bool mountainFlag;
                        int terrainHeight = CalculateHeight(worldX, worldZ, out mountainFlag);

                        // decide block type
                        BlockType blockType = BlockType.AIR;
                        bool isSurface = (worldY == terrainHeight);
                        bool isBelowSurface = (worldY < terrainHeight);
                        bool isFarBelow = (worldY < terrainHeight - dirtThickness);
                        bool isUnderwater = (terrainHeight < seaLevel
                                                 && worldY <= seaLevel
                                                 && worldY > terrainHeight);

                        if (isSurface)
                        {
                            if (worldY > seaLevel)
                                blockType = mountainFlag ? BlockType.STONE : BlockType.GRASS;
                            else if (worldY == seaLevel)
                                blockType = BlockType.SAND;
                            else if (worldY > seaLevel - sandFalloff)
                            {
                                // mix sand and dirt near shore
                                int r = Random.Shared.Next(sandFalloff);
                                blockType = (seaLevel - worldY < r)
                                            ? BlockType.SAND
                                            : BlockType.DIRT;
                            }
                            else
                                blockType = mountainFlag ? BlockType.STONE : BlockType.DIRT;
                        }
                        else if (isFarBelow)
                        {
                            blockType = BlockType.STONE;
                        }
                        else if (isBelowSurface)
                        {
                            blockType = mountainFlag ? BlockType.STONE : BlockType.DIRT;
                        }
                        else if (isUnderwater)
                        {
                            blockType = BlockType.WATER;
                        }

                        // write it
                        if (blockType != BlockType.AIR)
                            chunk.SetBlock(x, y, z, blockType);
                    }
                }
            }
        }

        public int CalculateHeight(int x, int z, out bool mountain)
        {
            float h = 5f * (noise.Noise(x * noiseScale / 4, z * noiseScale / 4) - 0.5f);
            float m = h;
            h += 4f * (noise.Noise(x * noiseScale / 3, z * noiseScale / 3) - 0.5f);
            h += 4f * (noise.Noise(x * noiseScale / 5, z * noiseScale / 5) - 0.5f);

            mountain = false;
            if (m > 0.7f)
            {
                h *= (h - 0.7f);
                mountain = true;
            }

            h = h * Chunk.SIZE - 1;
            if (h < 0)
                h = -MathF.Pow(-h, 0.8f);

            return (int)h;
        }
    }

}
