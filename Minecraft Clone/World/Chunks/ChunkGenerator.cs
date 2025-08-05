using Minecraft_Clone.World;
using Minecraft_Clone.World.Chunks;
using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Formats.Asn1.AsnWriter;

namespace Minecraft_Clone.World.Chunks
{
    public class ChunkGenerator
    {
        private PerlinNoise noise;
        private float noiseScale;
        public float surfaceThreshold = 0.5f;

        public PerlinNoise Noise { get => noise; set => noise = value; }

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

                        // Volumetric shaping
                        float density = (noise.FractalNoise(worldX * noiseScale, worldY * noiseScale, worldZ * noiseScale));

                        // influence density by double
                        density = density * 2;

                        var landBias = noise.Noise((float)worldX/ noiseScale, (float)worldZ/ noiseScale);
                        
                        //decide what kind of block this is.

                        if (worldY <= seaLevel)
                            type = BlockType.WATER;

                        // solid blocks
                        if (density - landBias * worldY/40f > (0.95f))
                        {
                            type = BlockType.STONE;

                            if (worldY > seaLevel && world.TryGetBlockAt((worldX, worldY + 1, worldZ), out var b))
                            {
                                if (b.isAir) type = BlockType.GRASS;
                                if (b.Type == BlockType.GRASS || b.Type == BlockType.DIRT)
                                {
                                    if (landBias > 0.30) type = BlockType.DIRT;
                                    else type = BlockType.STONE;
                                }
                            }

                            else if (worldY <= seaLevel && world.TryGetBlockAt((worldX, worldY + 1, worldZ), out var c))
                            {
                                if (!c.isSolid) type = BlockType.SAND;
                                if (c.Type == BlockType.SAND)
                                {
                                    if (landBias > 0.30) type = BlockType.SAND;
                                }
                            }
                        }

                        chunk.SetBlock(x, y, z, type);
                    }
                }
            }
        }
    }
}
