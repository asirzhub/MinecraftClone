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

        Random random;

        // <summary>
        /// Create a world using a seed and noise Scale
        /// </summary>
        public ChunkWorld(int seed = 0, float noiseScale = 0.1f)
        {
            this.seed = seed;
            noise = new PerlinNoise(seed);
            this.noiseScale = noiseScale;
            random = new Random(seed);
        }

        public int seaLevel;
        public int dirtThickness;
        public int sandFalloff;

        // <summary>
        /// Instance a chunk at the designated location, and add it to the world's chunks.
        /// </summary>
        public Chunk AddChunk(Vector3i pos, BlockType fillType = BlockType.AIR)
        {
            Chunk chunk = new Chunk(pos.X, pos.Y, pos.Z);
            chunk.FillWithBlock(fillType);
            chunks.Add(pos, chunk);
            return chunk;
        }

        // <summary>
        /// Probably not going to use this going forward but keeping here in case. Generate the world about a point with specific size.
        /// </summary>
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
                            for (int by = 0; by < Chunk.CHUNKSIZE; by++)
                            {
                                for (int bz = 0; bz < Chunk.CHUNKSIZE; bz++)
                                {
                                    Vector3i blockWorldPos = _chunk.ChunkPosition() * Chunk.CHUNKSIZE + (bx, by, bz);
                                    int h = (int)(3000 * ((noise.Noise(blockWorldPos.X * noiseScale, blockWorldPos.Z * noiseScale))
                                        * (0.5 * noise.Noise(blockWorldPos.Z * noiseScale / 2, blockWorldPos.X * noiseScale / 2))
                                        * (0.25 * noise.Noise(blockWorldPos.X * noiseScale / 4, blockWorldPos.Z * noiseScale / 4)))) - 50;

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

        // <summary>
        /// Generate the world about a point with specific size, asynchronously.
        /// </summary>
        public async Task GenerateWorldAsync(
            Vector3i origin,
            Vector3i size,
            int seaLevel,
            int dirtThickness,
            int sandFallof,
            IProgress<float> progress = null,
            CancellationToken token = default
        )
        {
            this.seaLevel = seaLevel;
            this.dirtThickness = dirtThickness;
            this.sandFalloff = sandFallof;
            // Run the CPU-heavy loop on a ThreadPool thread
            await Task.Run(() =>
            {
                int total = (2 * size.X) * (2 * size.Y) * (2 * size.Z);
                int done = 0;
                int r;
                // six nest loops!
                for (int chunkX = origin.X - size.X; chunkX < origin.X + size.X; chunkX++)
                {
                    for (int chunkZ = origin.Z - size.Z; chunkZ < origin.Z + size.Z; chunkZ++)
                    {
                        for (int chunkY = origin.Y - size.Y; chunkY < origin.Y + size.Y; chunkY++)
                        {
                            token.ThrowIfCancellationRequested();
                            Chunk _chunk = AddChunk((chunkX, chunkY, chunkZ), BlockType.AIR);

                            for (int blockX = 0; blockX < Chunk.CHUNKSIZE; blockX++)
                            {
                                for (int blockY = 0; blockY < Chunk.CHUNKSIZE; blockY++)
                                {
                                    for (int blockZ = 0; blockZ < Chunk.CHUNKSIZE; blockZ++)
                                    {
                                        Vector3i blockWorldPos = _chunk.ChunkPosition() * Chunk.CHUNKSIZE + (blockX, blockY, blockZ);
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
                                                blockType = (seaLevel - blockWorldPos.Y < R) ? BlockType.SAND : BlockType.DIRT;
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
                            done++;
                            progress?.Report((float)done / total);
                        }
                    }
                }
                    
            }, token);
            Console.WriteLine("worldgen completed");
        }

        public int CalculateHeight(int x, int z, out bool mountain)
        {
            float height = (5f * (noise.Noise(x * noiseScale / 4, z * noiseScale / 4) - 0.5f));
            float m = height;
            height += (4f * (noise.Noise(x * noiseScale / 3, z * noiseScale / 3) - 0.5f));
            height += (4f * (noise.Noise(x * noiseScale / 5, z * noiseScale / 5) - 0.5f));

            mountain = false;
            if (m > 0.7f)
            {
                height *= (height - 0.7f);
                mountain = true;
            }

            height *= Chunk.CHUNKSIZE;
            height -= 1;
            if (height < 0)
            {
                height = -MathF.Pow(-height, 0.8f);
            }
            return (int)height;
        }


    }
}