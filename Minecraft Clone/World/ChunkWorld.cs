using OpenTK.Mathematics;

namespace Minecraft_Clone.World
{
    public class ChunkWorld
    {
        // store chunks in a 3d dictionary, with vector3 (integers) perfect for our case
        public Dictionary<Vector3i, Chunk> chunks = new();

        int seed;
        float noiseScale;
        PerlinNoise noise; // The base for the world gen

        public ChunkWorld(int seed, float noiseScale)
        {
            this.seed = seed;
            noise = new PerlinNoise(seed);
            this.noiseScale = noiseScale;
        }

        public Chunk AddChunk(Vector3i pos, BlockType fillType = BlockType.AIR)
        {
            Chunk chunk = new Chunk(pos.X, pos.Y, pos.Z);
            chunk.FillWithBlock(fillType);
            chunks.Add(pos, chunk);
            return chunk;
        }

        public void GenerateWorldAbout(Vector3i origin, Vector3i size, int seaLevel, int dirtThickness)
        {
            for (int x = origin.X - size.X; x < origin.X + size.X; x++)
            {
                for (int z = origin.Z - size.Z; z < origin.Z + size.Z; z++)
                {
                    for (int y = origin.Y - size.Y; y < origin.Y + size.Y; y++)
                    {
                        Chunk _chunk = AddChunk((x, y, z), BlockType.AIR);
                        
                        for (int bx = 0; bx < Chunk.CHUNKSIZE; bx++)
                        {
                            for(int by = 0; by < Chunk.CHUNKSIZE; by++)
                            {
                                for(int bz = 0; bz < Chunk.CHUNKSIZE; bz++)
                                {
                                    Vector3i blockWorldPos = _chunk.ChunkPosition() * Chunk.CHUNKSIZE + (bx, by, bz);
                                    int h = (int)(3000 * ((noise.Noise(blockWorldPos.X * noiseScale, blockWorldPos.Z * noiseScale))
                                        * (0.5 * noise.Noise(blockWorldPos.Z * noiseScale / 2, blockWorldPos.X * noiseScale / 2))
                                        * (0.25 * noise.Noise(blockWorldPos.X * noiseScale / 4, blockWorldPos.Z * noiseScale / 4))))-50;
                                    
                                    if (blockWorldPos.Y == h) _chunk.SetBlock(bx, by, bz, BlockType.GRASS);
                                    else if (blockWorldPos.Y < h - dirtThickness) _chunk.SetBlock(bx, by, bz, BlockType.STONE);
                                    else if (blockWorldPos.Y < h) _chunk.SetBlock(bx, by, bz, BlockType.DIRT);
                                    else if (h < seaLevel && blockWorldPos.Y <= seaLevel && blockWorldPos.Y > h) _chunk.SetBlock(bx, by, bz, BlockType.WATER);
                                }
                            }
                        }

                    }
                }
            }
        }


    }
}