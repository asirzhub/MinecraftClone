using Minecraft_Clone.World.SurfaceFeatures;
using OpenTK.Mathematics;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Minecraft_Clone.World.Chunks.ChunkGenerator;
using static System.Formats.Asn1.AsnWriter;

namespace Minecraft_Clone.World.Chunks
{
    public class ChunkGenerator
    {
        // chunk block generation delivery class
        public class CompletedChunkBlocks
        {
            public Vector3i index;
            public bool isEmpty;
            public bool hasGrass;
            public byte[] blocks;
            
            public CompletedChunkBlocks(Vector3i index, byte[] blocks, bool isEmpty, bool hasGrass)
            {
                this.index = index;
                this.blocks = blocks;
                this.isEmpty = isEmpty;
                this.hasGrass = hasGrass;
            }

            public CompletedChunkBlocks(Vector3i index, Chunk chunk)
            {
                this.index = index;
                this.blocks = chunk.blocks;
                this.isEmpty = chunk.IsEmpty;
                this.hasGrass = chunk.hasGrass;
            }

            public void Dispose()
            {
                Array.Clear(this.blocks);
            }
        }

        // chunk's block generation kick-off fxn
        public Task GenerationTask(Vector3i index, CancellationTokenSource cts, WorldGenerator worldGenerator, ConcurrentQueue<CompletedChunkBlocks> queue)
        {
            // async wrapper for the long part of the operation
            return Task.Run(async () =>
            {
                var result = await GenerateBlocks(index, cts.Token, worldGenerator);
                queue.Enqueue(result);
            });
        }

        // generates the blocks for a given chunk and world generator 
        async Task<CompletedChunkBlocks> GenerateBlocks(Vector3i chunkIndex, CancellationToken token, WorldGenerator worldGenerator)
        {
            var tempChunk = new Chunk();

            for (int x = 0; x < Chunk.SIZE; x++)
            {
                for (int y = Chunk.SIZE - 1; y >= 0; y--)
                {
                    for (int z = 0; z < Chunk.SIZE; z++)
                    {
                        token.ThrowIfCancellationRequested();

                        int worldX = chunkIndex.X * Chunk.SIZE + x;
                        int worldY = chunkIndex.Y * Chunk.SIZE + y;
                        int worldZ = chunkIndex.Z * Chunk.SIZE + z;

                        tempChunk.SetBlock(x, y, z, worldGenerator.GetBlockAtWorldPos((worldX, worldY, worldZ)));
                    }
                }
            }

            return new CompletedChunkBlocks(chunkIndex, tempChunk.blocks, tempChunk.IsEmpty, tempChunk.hasGrass);
        }

        // chunk's feature generation kick-off fxn
        public Task FeatureAddingTask(Vector3i index, Chunk chunk, CancellationTokenSource cts, WorldGenerator worldGenerator, ConcurrentQueue<CompletedChunkBlocks> queue)
        {
            // async wrapper for the long part of the operation
            return Task.Run(async () =>
            {
                queue.Enqueue(await GenerateFeatures(index, chunk, cts.Token, worldGenerator));
            });
        }

        Tree tree = new Tree(102020);

        // generates the blocks for a given chunk and world generator 
        async Task<CompletedChunkBlocks> GenerateFeatures(Vector3i index, Chunk chunk, CancellationToken token, WorldGenerator worldGenerator)
        {
            if(chunk.GetHeightAtXZ((Chunk.SIZE/2, Chunk.SIZE/2), out var h))
            {
                Vector3i scale = (5, 6, 5);
                // create tree blocks
                tree.GenerateTreeBlocks((5, 6, 5));

                Vector3i coordinateOffset = new Vector3i(Chunk.SIZE / 2 - tree.scale.X / 2,
                    h,
                    Chunk.SIZE / 2 - tree.scale.Z / 2);

                // place the tree into the chunk
                for (byte x = 0; x < tree.scale.X; x++)
                {
                    for (byte y = 0; y < tree.scale.Y; y++)
                    {
                        for (byte z = 0; z < tree.scale.Z; z++)
                        {
                            Vector3i localCoord = (x, y, z) + coordinateOffset;
                            BlockType treeBlock = (BlockType)tree.blocks[(y * scale.Z + z) * scale.X + x];
                            if (treeBlock != BlockType.AIR && 
                                localCoord.X >= 0 && localCoord.X <Chunk.SIZE &&
                                localCoord.Y >= 0 && localCoord.Y < Chunk.SIZE &&
                                localCoord.Z >= 0 && localCoord.Z < Chunk.SIZE)
                                chunk.SetBlock(localCoord, treeBlock);
                        }
                    }
                }
                return new CompletedChunkBlocks(index, chunk);
            }
            else
            {
                return new CompletedChunkBlocks(index, chunk);
            }
        }
    }
}
