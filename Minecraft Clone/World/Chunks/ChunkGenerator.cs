using Minecraft_Clone.World.SurfaceFeatures;
using OpenTK.Mathematics;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Minecraft_Clone.World.Chunks.ChunkGenerator;

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
        public Task FeatureAddingTask(CompletedChunkBlocks completedChunkBlocks, CancellationTokenSource cts, WorldGenerator worldGenerator, ConcurrentQueue<CompletedChunkBlocks> queue)
        {
            // async wrapper for the long part of the operation
            return Task.Run(async () =>
            {
                var result = await GenerateFeatures(completedChunkBlocks, cts.Token, worldGenerator);
                queue.Enqueue(result);
            });
        }

        // generates the blocks for a given chunk and world generator 
        async Task<CompletedChunkBlocks> GenerateFeatures(CompletedChunkBlocks completedChunkBlocks, CancellationToken token, WorldGenerator worldGenerator)
        {
            return completedChunkBlocks;
        }

        // surface feature: tree
        private Tree tree = new(-1);

        public void GrowTrees(Vector3i chunkIdx, Chunk chunk, int count = 1)
        {
            Vector3i treeRootLocalCoord = (Chunk.SIZE / 2, Chunk.SIZE / 2, Chunk.SIZE / 2);
            while (count > 0)
            {
                count--;
                Vector3i scale = (5, 6, 5);
                tree.GrowTree(scale);
                tree.RNG.Next(chunkIdx.X * chunkIdx.Y + chunkIdx.Z * chunkIdx.Y);

                for (int x = 0; x < scale.X; x++)
                {
                    for (int y = 0; y < scale.Y; y++)
                    {
                        for (int z = 0; z < scale.Z; z++)
                        {
                            var block = tree.blocks[(y * scale.Y + z) * scale.Z + x];
                            // transform from tree coordinates to chunk coordinates
                            chunk.SetBlock(treeRootLocalCoord, (BlockType)block);
                        }
                    }
                }
            }
        }
    }
}
