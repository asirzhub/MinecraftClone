using Minecraft_Clone;
using Minecraft_Clone.Graphics;
using Minecraft_Clone.World;
using Minecraft_Clone.World.Chunks;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using System.Collections.Concurrent;
using System;
using static Minecraft_Clone.Graphics.VBO;
using System.Diagnostics;

public class ChunkManager
{
    // needs no introduction
    public ChunkRenderer renderer = new ChunkRenderer();
    public WorldGenerator worldGenerator = new WorldGenerator(512);

    public Vector3i currentChunkIndex = new();
    public int radius = 9;
    public int maxChunkTasks;
    public bool chunkTasksHalved = false;
    public float chunkTaskHalftime = 20f;// after 20 seconds, half the chunk tasks (prioritize speed at first, then smoothness)
    public float expiryTime = 2f;

    // store every chunk instance in Chunks:
    ConcurrentDictionary<Vector3i, Chunk> ActiveChunks = new ConcurrentDictionary<Vector3i, Chunk>();
    List<Vector3i> ActiveChunksIndices = new List<Vector3i>();
    List<Vector3i> LastActivationChunksIndices = new List<Vector3i>();

    // also store a list for chunks that are about to be deleted from memory (hysterisis/thrashing management)
    ConcurrentDictionary<Vector3i, float> ExpiredChunkLifetimes = new ConcurrentDictionary<Vector3i, float>();

    // worker result classes, one for mesh worker and one for chunk generation worker
    ConcurrentQueue<CompletedChunkBlocks> CompletedBlocksQueue = new();
    ConcurrentQueue<CompletedMesh> CompletedMeshQueue = new();

    // worker tasks list
    ConcurrentDictionary<Vector3i, Task> RunningTasks = new();
    ConcurrentDictionary<Vector3i, CancellationTokenSource> RunningTasksCTS = new();

    public int taskCount => RunningTasks.Count;

    public ChunkManager()
    {
        maxChunkTasks = (int)(Environment.ProcessorCount -1);
    }

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
    public Task GenerationTask(Vector3i index)
    {
        ActiveChunks[index].SetState(ChunkState.GENERATING);
        CancellationTokenSource cts = new CancellationTokenSource();
        RunningTasksCTS.TryAdd(index, cts);

        return Task.Run(async () =>
        {

            var result = await GenerateBlocks(index, cts.Token);
            CompletedBlocksQueue.Enqueue(result);

            //RunningTasks.TryRemove(index, out _);
            //RunningTasksCTS.TryRemove(index, out _);
        });
    }
    Task<CompletedChunkBlocks> GenerateBlocks(Vector3i chunkIndex, CancellationToken token)
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

    // chunk mesh delivery class
    public class CompletedMesh
    {
        public Vector3i index;
        public MeshData solidMesh;
        public MeshData liquidMesh;

        public CompletedMesh(Vector3i index, MeshData solidMesh, MeshData liquidMesh)
        {
            this.index = index;
            this.solidMesh = solidMesh;
            this.liquidMesh = liquidMesh;
        }

        public void Dispose()
        {
            this.solidMesh.Dispose();
            this.liquidMesh.Dispose();
        }
    };
    // chunk mesher kick-off fxn
    public Task MeshTask(Vector3i index)
    {
        Chunk thisChunk = ActiveChunks[index];
        thisChunk.SetState(ChunkState.MESHING);

        // skip meshing if it's empty
        if (thisChunk.IsEmpty)
        {
            CompletedMesh result = new CompletedMesh(index, new MeshData(), new MeshData());
            CompletedMeshQueue.Enqueue(result);
            return Task.CompletedTask;
        }

        CancellationTokenSource cts = new CancellationTokenSource();
        RunningTasksCTS.TryAdd(index, cts);

        return Task.Run(async () =>
        {

            var result = await BuildMesh(index, ChunkList(), cts.Token);

            CompletedMeshQueue.Enqueue(result);

            //RunningTasks.TryRemove(index, out _);
            //RunningTasksCTS.TryRemove(index, out _);
        });
    }
    Task<CompletedMesh> BuildMesh(Vector3i chunkIndex, List<Chunk> chunks, CancellationToken token)
    {
        // no remeshing visible chunks
        bool thisChunkExists = TryGetChunkAtIndex(chunkIndex, out var chunk);
        return Task.Run(async () =>
        {
            Vector3i localOrigin = chunkIndex * Chunk.SIZE;
            bool forwardChunkExists = TryGetChunkAtIndex(chunkIndex + new Vector3i(0, 0, 1), out Chunk forwardChunk);
            bool rearChunkExists = TryGetChunkAtIndex(chunkIndex + new Vector3i(0, 0, -1), out Chunk rearChunk);
            bool leftChunkExists = TryGetChunkAtIndex(chunkIndex + new Vector3i(-1, 0, 0), out Chunk leftChunk);
            bool rightChunkExists = TryGetChunkAtIndex(chunkIndex + new Vector3i(+1, 0, 0), out Chunk rightChunk);
            bool topChunkExists = TryGetChunkAtIndex(chunkIndex + new Vector3i(0, +1, 0), out Chunk topChunk);
            bool belowChunkExists = TryGetChunkAtIndex(chunkIndex + new Vector3i(0, -1, 0), out Chunk belowChunk);

            int seaLevel = worldGenerator.seaLevel;

            MeshData solidResult = new MeshData();
            uint solidVertexOffset = 0;

            MeshData liquidResult = new MeshData();
            uint liquidVertexOffset = 0;

            // local chunk coordinate 
            for (byte x = 0; x < Chunk.SIZE; x++)
            {
                for (byte y = 0; y < Chunk.SIZE; y++)
                {
                    for (byte z = 0; z < Chunk.SIZE; z++)
                    {
                        token.ThrowIfCancellationRequested();

                        Block block = chunk.GetBlock(x, y, z);
                        if (block.Type == BlockType.AIR) // skip air
                            continue;

                        Vector3i blockWorldPos = localOrigin + (x, y, z);

                        // base-layer culling: hide faces hidden by solid blocks
                        bool[] occlusions = new bool[6];

                        // front
                        if (TryGetBlockAtWorldPosition(blockWorldPos + new Vector3i(0, 0, +1), out var bF))
                            occlusions[0] = bF.isSolid || (block.isWater && bF.isWater);

                        // back
                        if (TryGetBlockAtWorldPosition(blockWorldPos + new Vector3i(0, 0, -1), out var bB))
                            occlusions[1] = bB.isSolid || (block.isWater && bB.isWater);

                        // left
                        if (TryGetBlockAtWorldPosition(blockWorldPos + new Vector3i(-1, 0, 0), out var bL))
                            occlusions[2] = bL.isSolid || (block.isWater && bL.isWater);

                        // right
                        if (TryGetBlockAtWorldPosition(blockWorldPos + new Vector3i(+1, 0, 0), out var bR))
                            occlusions[3] = bR.isSolid || (block.isWater && bR.isWater);

                        // up
                        if (TryGetBlockAtWorldPosition(blockWorldPos + new Vector3i(0, 1, 0), out var bU))
                            occlusions[4] = bU.isSolid || (block.isWater && bU.isWater); ;

                        // down
                        if (TryGetBlockAtWorldPosition(blockWorldPos + new Vector3i(0, -1, 0), out var bD))
                            occlusions[5] = bD.isSolid || (block.isWater && bD.isWater);

                        // for each face
                        foreach (CubeMesh.Face face in Enum.GetValues(typeof(CubeMesh.Face)))
                        {
                            int faceIndex = (int)face;
                            if (occlusions[faceIndex]) continue;              // skip hidden faces
                            var faceVerts = CubeMesh.PackedFaceVertices[face];

                            // Append 4 corners
                            for (int i = 0; i < 4; i++)
                            {
                                var v = faceVerts[i];

                                // Local position within chunk (0..16)
                                var vPos = v.Position();
                                byte lx = (byte)(x + vPos.X);
                                byte ly = (byte)(y + vPos.Y);
                                byte lz = (byte)(z + vPos.Z);

                                // Compute UV
                                var faceUVs = BlockRegistry.Types[block.Type].FaceUVs;
                                Vector2 tile = faceUVs[faceIndex];
                                Vector2 uv = (tile + (v.TexU, v.TexV)) / 8f;
                                uv.Y = 1f - uv.Y;

                                byte normal = (byte)face;

                                byte lightLevel = 15;

                                if (blockWorldPos.Y < seaLevel && blockWorldPos.Y > seaLevel - 6)
                                {
                                    lightLevel = (byte)(15 - (seaLevel - blockWorldPos.Y));
                                }
                                else if (blockWorldPos.Y <= seaLevel - 6)
                                {
                                    lightLevel = (byte)9;
                                }

                                // check the edges in the direction of the vertex to do ambient occlusion with
                                Vector3i[] AOCheckDirection = new Vector3i[4];
                                AOCheckDirection[0] = ((int)(MathF.Round((vPos.X - 0.5f) * 2f)),
                                    0,
                                    (int)(MathF.Round((vPos.Z - 0.5f) * 2f)));

                                AOCheckDirection[1] = ((int)(MathF.Round((vPos.X - 0.5f) * 2f)),
                                    (int)(MathF.Round((vPos.Y - 0.5f) * 2f)),
                                    0);

                                AOCheckDirection[2] = (0,
                                    (int)(MathF.Round((vPos.Y - 0.5f) * 2f)),
                                    (int)(MathF.Round((vPos.Z - 0.5f) * 2f)));

                                AOCheckDirection[3] = ((int)(MathF.Round((vPos.X - 0.5f) * 2f)),
                                    (int)(MathF.Round((vPos.Y - 0.5f) * 2f)),
                                    (int)(MathF.Round((vPos.Z - 0.5f) * 2f)));

                                byte occluderCount = 0;
                                foreach (var direction in AOCheckDirection)
                                {
                                    if (lightLevel > 1)
                                    {
                                        TryGetBlockAtWorldPosition(blockWorldPos + direction, out var b);
                                        if (b.isSolid)
                                        {
                                            occluderCount += (byte)1;
                                        }
                                    }
                                }

                                if (occluderCount > 2) lightLevel -= occluderCount;//ambient occlusion can only happen with two or more occluders

                                if (block.isWater)
                                {
                                    liquidResult.Vertices.Add(
                                        new PackedVertex(lx, ly, lz, uv.X, uv.Y, normal, lightLevel, true)
                                    );

                                    // Two triangles (0,1,2) & (2,3,0)
                                    liquidResult.Indices.Add(liquidVertexOffset + 0);
                                    liquidResult.Indices.Add(liquidVertexOffset + 1);
                                    liquidResult.Indices.Add(liquidVertexOffset + 2);
                                    liquidResult.Indices.Add(liquidVertexOffset + 2);
                                    liquidResult.Indices.Add(liquidVertexOffset + 3);
                                    liquidResult.Indices.Add(liquidVertexOffset + 0);

                                    liquidVertexOffset += 4;
                                }
                                else
                                {
                                    solidResult.Vertices.Add(
                                        new PackedVertex(lx, ly, lz, uv.X, uv.Y, normal, lightLevel)
                                    );

                                    // Two triangles (0,1,2) & (2,3,0)
                                    solidResult.Indices.Add(solidVertexOffset + 0);
                                    solidResult.Indices.Add(solidVertexOffset + 1);
                                    solidResult.Indices.Add(solidVertexOffset + 2);
                                    solidResult.Indices.Add(solidVertexOffset + 2);
                                    solidResult.Indices.Add(solidVertexOffset + 3);
                                    solidResult.Indices.Add(solidVertexOffset + 0);

                                    solidVertexOffset += 4;
                                }
                            }
                        }
                    }
                }
            }
            return new CompletedMesh(chunkIndex, solidResult, liquidResult);
        });
    }

    public int totalRenderCalls = 0;

    public void Update(Camera camera, float frameTime, float time, Vector3 sunDirection)
    {
        bool updateIndices = (time % 5f < 0.01f);

        totalRenderCalls = 0;
        if (!chunkTasksHalved)
        {
            chunkTaskHalftime -= frameTime;
            if (chunkTaskHalftime < 0)
            {
                chunkTasksHalved = true;
                maxChunkTasks /= 2;
            }
        }

        if(updateIndices)
            ActiveChunksIndices = ListActiveChunksIndices(camera.position, radius, radius / 2);

        currentChunkIndex = WorldPosToChunkIndex((
                                (int)MathF.Floor(camera.position.X),
                                (int)MathF.Floor(camera.position.Y),
                                (int)MathF.Floor(camera.position.Z))); ; // first index in the list is always the center index  

        // for each CompletedChunkBlocks, move data from the queue into the respective chunk. MARK STATE
        while (CompletedBlocksQueue.TryDequeue(out var result))
        {
            //Console.WriteLine(result.index);
            Chunk c = ActiveChunks[result.index];
            c.blocks = result.blocks;
            c.IsEmpty = result.isEmpty;
            c.SetState(ChunkState.GENERATED);
            RunningTasks.TryRemove(result.index, out _);
            //if (c.IsEmpty) Console.WriteLine(result.index + " was empty!");
        }

        // for each CompletedMesh, move data from the queue into the respective chunk. MARK STATE
        while (CompletedMeshQueue.TryDequeue(out var result))
        {
            Chunk c = ActiveChunks[result.index];
            c.solidMesh = result.solidMesh;
            c.liquidMesh = result.liquidMesh;
            c.SetState(ChunkState.MESHED);
            RunningTasks.TryRemove(result.index, out _);
        }

        foreach (var idx in ActiveChunksIndices)
        {
            if (ActiveChunks.TryGetValue(idx, out var resultChunk))
            {
                var state = resultChunk.GetState();

                // un-expire chunks that were expired but need to be brought back before expiry
                if (ExpiredChunkLifetimes.ContainsKey(idx))
                    ExpiredChunkLifetimes.TryRemove(idx, out _);

                switch (state)
                {
                    case ChunkState.BIRTH:
                        if (RunningTasks.Count < maxChunkTasks)
                            RunningTasks.TryAdd(idx, GenerationTask(idx));

                        break;
                    case ChunkState.GENERATED:
                        if (AreNeighborsGenerated(idx) && RunningTasks.Count < maxChunkTasks)
                            RunningTasks.TryAdd(idx, MeshTask(idx));
                        break;

                    case ChunkState.MESHED:
                        resultChunk.SetState(ChunkState.VISIBLE);
                        if (resultChunk.IsEmpty)
                            resultChunk.SetState(ChunkState.INVISIBLE);
                        break;

                    case ChunkState.VISIBLE:
                        var rendered = renderer.RenderChunk(resultChunk.solidMesh, camera, idx, time, sunDirection);
                        if (rendered) totalRenderCalls++;
                        break;
                    default:
                        break;
                }
            }
            else
                ActiveChunks.TryAdd(idx, new Chunk());
        }

        // render water with no depth mask, after all solids were rendered
        GL.DepthMask(false);
        foreach (var ChunkKVP in ActiveChunks)
            if (ChunkKVP.Value.GetState() == ChunkState.VISIBLE)
                renderer.RenderChunk(ChunkKVP.Value.liquidMesh, camera, ChunkKVP.Key, time, sunDirection);

        GL.DepthMask(true);

        // assign expired chunks
        foreach (var idx in LastActivationChunksIndices)
        {
            if (!ActiveChunksIndices.Contains(idx))
                MarkExpired(idx);
        }

        // for each chunk in the expiry list, check if it's expired. if it is, dispose it from ram
        foreach (var kvp in ExpiredChunkLifetimes)
        {
            if (kvp.Value > expiryTime)
            {
                ExpiredChunkLifetimes.TryRemove(kvp.Key, out _);
                DisposeChunk(kvp.Key);
            }
            // unexpired chunks will wait a little longer
            else
                ExpiredChunkLifetimes[kvp.Key] = kvp.Value + frameTime;
        }

        if (updateIndices)
            LastActivationChunksIndices = new List<Vector3i>(ActiveChunksIndices);
    }

    public void MarkExpired(Vector3i idx)
    {
        ExpiredChunkLifetimes.TryAdd(idx, 0f);
    }

    // returns true if the 6 adjacent chunks are at least at the generated state (false if birth/generating)
    public bool AreNeighborsGenerated(Vector3i centerIndex)
    {
        Vector3i[] directions = { Vector3i.UnitX, -Vector3i.UnitX,
                                Vector3i.UnitY, -Vector3i.UnitY,
                                Vector3i.UnitZ, -Vector3i.UnitZ,};

        foreach (var dir in directions)
        {
            bool exists = TryGetChunkAtIndex(centerIndex + dir, out var c);
            if (!exists || !c.NeighborReady)
                return false;
        }

        return true;
    }

    public void DisposeChunk(Vector3i index)
    {
        if (ActiveChunks.TryRemove(index, out var chunk))
        {
            chunk.blocks = null;  // release block storage
            chunk.IsEmpty = true;
            chunk.DisposeMeshes();

            if (RunningTasksCTS.TryGetValue(index, out var cts))
                cts.CancelAsync();
            if (RunningTasks.TryGetValue(index, out var task))
                task.Dispose();
        }
    }

    // generates the list of chunk indices that need to be ready. ordered to start at the center index and spiral out.
    public List<Vector3i> ListActiveChunksIndices(Vector3 centerWorldPos, int horizontalRadius, int verticalRadius)
    {
        List<Vector3i> result = new();
        Vector3i centerIndex = currentChunkIndex;

        HashSet<Vector3i> visited = new();
        Queue<Vector3i> queue = new();

        // Start from center
        queue.Enqueue(centerIndex);
        visited.Add(centerIndex);

        Vector3i[] directions =
        {   new Vector3i( 1, 0, 0),
            new Vector3i(-1, 0, 0),
            new Vector3i( 0, 1, 0),
            new Vector3i( 0,-1, 0),
            new Vector3i( 0, 0, 1),
            new Vector3i( 0, 0,-1),
        };

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            result.Add(current);

            foreach (var dir in directions)
            {
                var next = current + dir;
                Vector3i delta = next - centerIndex;

                // Bound check
                if (Math.Abs(delta.X) <= horizontalRadius &&
                    Math.Abs(delta.Z) <= horizontalRadius &&
                    Math.Abs(delta.Y) <= verticalRadius &&
                    visited.Add(next)) // only enqueue if not seen
                {
                    queue.Enqueue(next);
                }
            }
        }
        
        return result;
    }

    public bool TryGetChunkAtIndex(Vector3i index, out Chunk result)
    {
        result = default;

        var exists = ActiveChunks.TryGetValue(index, out var chunk);

        if (exists)
        {
#pragma warning disable CS8601 // Possible null reference assignment.
            result = chunk;
#pragma warning restore CS8601 // Possible null reference assignment.
            return true;
        }
        return false;
    }

    public Vector3i WorldPosToChunkIndex(Vector3i worldIndex, int chunkSize = Chunk.SIZE)
    {
        int chunkX = (int)Math.Floor(worldIndex.X / (double)chunkSize);
        int chunkY = (int)Math.Floor(worldIndex.Y / (double)chunkSize);
        int chunkZ = (int)Math.Floor(worldIndex.Z / (double)chunkSize);
        return new Vector3i(chunkX, chunkY, chunkZ);
    }

    public bool TryGetBlockAtWorldPosition(Vector3i worldIndex, out Block result, int chunkSize = Chunk.SIZE)
    {
        result = default;

        Vector3i chunkIndex = WorldPosToChunkIndex(worldIndex, chunkSize);

        Vector3i localBlockPos = new Vector3i(
            worldIndex.X - chunkIndex.X * chunkSize,
            worldIndex.Y - chunkIndex.Y * chunkSize,
            worldIndex.Z - chunkIndex.Z * chunkSize
        );

        // sanity check (should not be necessary if floor division is correct)
        if (localBlockPos.X < 0 || localBlockPos.X >= chunkSize ||
            localBlockPos.Y < 0 || localBlockPos.Y >= chunkSize ||
            localBlockPos.Z < 0 || localBlockPos.Z >= chunkSize)
        {
            return false;
        }

        if (TryGetChunkAtIndex(chunkIndex, out Chunk targetChunk))
        {
            result = targetChunk.GetBlock(localBlockPos.X, localBlockPos.Y, localBlockPos.Z);
            return true;
        }
        return false;
    }

    // translates the existing chunk dictionary int just chunks list
    List<Chunk> ChunkList()
    {
        var result = new List<Chunk>();
        foreach (var chunk in ActiveChunks.Values)
            result.Add(chunk);

        return result;
    }
}
