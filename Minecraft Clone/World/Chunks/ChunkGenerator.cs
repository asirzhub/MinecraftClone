using OpenTK.Mathematics;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Minecraft_Clone.World.Chunks
{
    public class ChunkGenerator
    {
        // chunk block generation delivery class
        public class CompletedChunkBlocks
        {
            public Vector3i index;
            public bool isEmpty;
            public byte[] blocks;

            public CompletedChunkBlocks(Vector3i index, byte[] blocks, bool isEmpty)
            {
                this.index = index;
                this.blocks = blocks;
                this.isEmpty = isEmpty;
            }

            public void Dispose()
            {
                Array.Clear(this.blocks);
            }
        }
        // chunk's block generation kick-off fxn
        public Task GenerationTask(Vector3i index, CancellationTokenSource cts, WorldGenerator worldGenerator, ConcurrentQueue<CompletedChunkBlocks> queue)
        {

            return Task.Run(async () =>
            {

                var result = await GenerateBlocks(index, cts.Token, worldGenerator);

                //OnComplete.Invoke();
                queue.Enqueue(result);
                //RunningTasks.TryRemove(index, out _);
                //RunningTasksCTS.TryRemove(index, out _);
            });
        }
        Task<CompletedChunkBlocks> GenerateBlocks(Vector3i chunkIndex, CancellationToken token, WorldGenerator worldGenerator)
        {
            var tempChunk = new Chunk();

            return Task.Run(() =>
            {
                int totalBlocks = 0;

                // for each block in the 16×16×16 volume...
                for (int x = 0; x < Chunk.SIZE; x++)
                {
                    for (int y = Chunk.SIZE - 1; y >= 0; y--)
                    {
                        for (int z = 0; z < Chunk.SIZE; z++)
                        {
                            token.ThrowIfCancellationRequested();

                            // compute world-space coordinate of this block
                            int worldX = chunkIndex.X * Chunk.SIZE + x;
                            int worldY = chunkIndex.Y * Chunk.SIZE + y;
                            int worldZ = chunkIndex.Z * Chunk.SIZE + z;

                            // offload world gen code to the generator. facade pattern
                            BlockType type = worldGenerator.GetBlockAtWorldPos((worldX, worldY, worldZ));

                            tempChunk.SetBlock(x, y, z, type);
                            if (type != BlockType.AIR) totalBlocks++;
                        }
                    }
                }
                return new CompletedChunkBlocks(chunkIndex, tempChunk.blocks, tempChunk.IsEmpty);
            });
        }

    }
}
