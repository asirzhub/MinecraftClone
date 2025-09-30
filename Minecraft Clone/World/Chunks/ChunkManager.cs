using Minecraft_Clone;
using Minecraft_Clone.Graphics;
using Minecraft_Clone.World;
using Minecraft_Clone.World.Chunks;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using SixLabors.ImageSharp.PixelFormats;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using static Minecraft_Clone.Graphics.VBO;

public class ChunkManager
{
    // needs no introduction
    public ChunkRenderer renderer = new ChunkRenderer();
    public ChunkGenerator generator = new ChunkGenerator();
    public ChunkMesher mesher = new ChunkMesher();
    public WorldGenerator worldGenerator = new WorldGenerator(512);

    public Vector3i currentChunkIndex = new();
    public Vector3i lastChunkIndex = new();
    public int radius = 9;
    public int maxChunkTasks;
    public bool chunkTasksHalved = false;
    public float chunkTaskHalftime = 20f;// after 20 seconds, half the chunk tasks (prioritize speed at first, then smoothness)
    public float expiryTime = 2f; // how long a chunk can be "inactive" before disposed from ram?

    // independant lists to keep track of chunks that actually matter
    List<Vector3i> ActiveChunksIndices = new List<Vector3i>();
    List<Vector3i> LastActivationChunksIndices = new List<Vector3i>();

    // store every chunk instance in Chunks:
    ConcurrentDictionary<Vector3i, Chunk> ActiveChunks = new ConcurrentDictionary<Vector3i, Chunk>();
    
    // also store a list for chunks that are about to be deleted from memory (hysterisis/thrashing management)
    ConcurrentDictionary<Vector3i, float> ExpiredChunkLifetimes = new ConcurrentDictionary<Vector3i, float>();

    // worker threads deposit results in these
    ConcurrentQueue<ChunkGenerator.CompletedChunkBlocks> CompletedBlocksQueue = new();
    ConcurrentQueue<ChunkMesher.CompletedMesh> CompletedMeshQueue = new();

    // worker tasks list to control the number of worker threads
    ConcurrentDictionary<Vector3i, Task> RunningTasks = new();
    ConcurrentDictionary<Vector3i, CancellationTokenSource> RunningTasksCTS = new();

    public int taskCount => RunningTasks.Count;

    public ChunkManager(Camera camera)
    {
        // idk if this is the best way might change later
        maxChunkTasks = (int)(Environment.ProcessorCount -1);

        currentChunkIndex = WorldPosToChunkIndex((
                                (int)MathF.Floor(camera.position.X),
                                (int)MathF.Floor(camera.position.Y),
                                (int)MathF.Floor(camera.position.Z))); ; // first index in the list is always the center index  

        worldGenerator.PreComputeTreeLocations(currentChunkIndex, radius);
    }


    // fun stat
    public int totalRenderCalls = 0;
    public void Update(Camera camera, float frameTime, float time, Vector3 sunDirection, SkyRender sky)
    {
        totalRenderCalls = 0;

        // reduce chunk tasks after first burst 
        if (!chunkTasksHalved)
        {
            chunkTaskHalftime -= frameTime;
            if (chunkTaskHalftime < 0)
            {
                chunkTasksHalved = true;
                maxChunkTasks /= 2;
            }
        }

        bool updateIndices = currentChunkIndex != lastChunkIndex; // relevant chunk indices refreshed every 2 seconds

        if (updateIndices)
            ActiveChunksIndices = ListActiveChunksIndices(camera.position, radius, radius / 2);

        currentChunkIndex = WorldPosToChunkIndex((
                                (int)MathF.Floor(camera.position.X),
                                (int)MathF.Floor(camera.position.Y),
                                (int)MathF.Floor(camera.position.Z))); ; // first index in the list is always the center index  

        // for each CompletedChunkBlocks, move data from the queue into the respective chunk. MARK STATE
        while (CompletedBlocksQueue.TryDequeue(out var completedBlocks))
        {
            Chunk targetChunk = ActiveChunks[completedBlocks.index];
            targetChunk.blocks = completedBlocks.blocks;
            targetChunk.IsEmpty = completedBlocks.isEmpty;
            RunningTasks.TryRemove(completedBlocks.index, out _);

            // if the chunk's block data both generated AND featured, mark it so
            if (completedBlocks.featured)
            {
                targetChunk.SetState(ChunkState.FEATURED);
            }
            else // otherwise this queue item is only generated and not featured. need to mark it for a feature task
            {
                targetChunk.SetState(ChunkState.GENERATED);
            }

        }

        // for each CompletedMesh, move data from the queue into the respective chunk. MARK STATE
        while (CompletedMeshQueue.TryDequeue(out var resultChunk))
        {
            Chunk targetChunk = ActiveChunks[resultChunk.index];
            targetChunk.solidMesh = resultChunk.solidMesh;
            targetChunk.liquidMesh = resultChunk.liquidMesh;
            targetChunk.SetState(ChunkState.MESHED);
            RunningTasks.TryRemove(resultChunk.index, out _);
        }

        renderer.Bind(); // by binding only once (rather than every call), gain 10% FPS!

        // for every chunk that's still relevant
        foreach (var idx in ActiveChunksIndices)
        {
            // if the chunk has existed already
            if (ActiveChunks.TryGetValue(idx, out var resultChunk))
            {
                var state = resultChunk.GetState();

                // un-expire chunks that were expired but need to be brought back before expiry
                if (ExpiredChunkLifetimes.ContainsKey(idx))
                    ExpiredChunkLifetimes.TryRemove(idx, out _);

                // determine if the chunk is in view or not (for culling)
                if(state == ChunkState.VISIBLE || state == ChunkState.INVISIBLE)
                        resultChunk.SetState(IsChunkInView(camera, idx)? ChunkState.VISIBLE : ChunkState.INVISIBLE);

                // different instructions for different chunk states
                switch (state)
                {
                    case ChunkState.BIRTH:
                        // if theres room to add a generation job for a new chunk, do it
                        if (RunningTasks.Count < maxChunkTasks)
                        {
                            resultChunk.SetState(ChunkState.GENERATING);
                            CancellationTokenSource cts = new CancellationTokenSource();
                            RunningTasksCTS.TryAdd(idx, cts);
                            RunningTasks.TryAdd(idx, generator.GenerationTask(idx, cts, worldGenerator, CompletedBlocksQueue));
                        }

                        break;
                    case ChunkState.GENERATED:
                        // if the chunk has blocks neighbors with blocks and theres room for a task, make mesh for it
                        if (AreNeighborsGenerated(idx) && RunningTasks.Count < maxChunkTasks)
                        {
                            resultChunk.SetState(ChunkState.FEATURING);
                            CancellationTokenSource cts = new CancellationTokenSource();
                            RunningTasksCTS.TryAdd(idx, cts);
                            RunningTasks.TryAdd(idx, generator.FeatureTask(new ChunkGenerator.CompletedChunkBlocks(idx, resultChunk), cts, worldGenerator, CompletedBlocksQueue));
                        }
                        break;
                    case ChunkState.FEATURED:
                        // if the chunk has blocks neighbors with blocks and theres room for a task, make mesh for it
                        if (AreNeighborsGenerated(idx) && RunningTasks.Count < maxChunkTasks)
                        {
                            resultChunk.SetState(ChunkState.MESHING);
                            CancellationTokenSource cts = new CancellationTokenSource();
                            RunningTasksCTS.TryAdd(idx, cts);
                            RunningTasks.TryAdd(idx, mesher.MeshTask(idx, this, cts, CompletedMeshQueue, ActiveChunks, ChunkList(), LOD: 1));
                        }
                        break;
                    case ChunkState.MESHED:
                        // mark chunks with no mesh (all air) as invisible
                        resultChunk.SetState(ChunkState.VISIBLE);
                        if (resultChunk.IsEmpty)
                            resultChunk.SetState(ChunkState.INVISIBLE);
                        break;

                    case ChunkState.VISIBLE:
                        // render visible chunks, count how many
                        var rendered = renderer.RenderChunk(resultChunk.solidMesh, camera, idx, time, sunDirection, sky);
                        if (rendered) totalRenderCalls+=1;
                        break;
                    default:
                        break;
                }
            }
            else
                ActiveChunks.TryAdd(idx, new Chunk()); // adding a brand new chunk to the system
        }

        // render water with no depth mask, after all solids were rendered
        GL.DepthMask(false);
        foreach (var ChunkKVP in ActiveChunks)
            if (ChunkKVP.Value.GetState() == ChunkState.VISIBLE)
            {
                var rendered = renderer.RenderChunk(ChunkKVP.Value.liquidMesh, camera, ChunkKVP.Key, time, sunDirection, sky);
                if(rendered) totalRenderCalls += 1;
            }

        GL.DepthMask(true);

        // assign expired chunks to expiry list
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
        {
            LastActivationChunksIndices = new List<Vector3i>(ActiveChunksIndices);
            lastChunkIndex = currentChunkIndex;
        }

        worldGenerator.Update();
    }

    // naive frustrum culling using a cone frustrum
    public bool IsChunkInView(Camera camera, Vector3i idx)
    {
        Vector3 chunkWorldCoord = idx * Chunk.SIZE + Vector3.One * Chunk.SIZE/2; // center of the chunk
        Vector3 chunkToCamera = chunkWorldCoord - camera.position;

        if (chunkToCamera.LengthFast < 2 * Chunk.SIZE) // if the chunk is too close, exit out with true
            return true;

        float angle = Vector3.CalculateAngle(camera.front, chunkToCamera);

        if (angle < 1.2f) // if within view, true
            return true;

        return false;
    }

    // idk why i thought this was easier but whatever might need it later on
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

    // chunk disposal involves deleting all the chunk information from ram
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

        // BFS chunk selector uses the queue
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            result.Add(current);

            foreach (var dir in directions)
            {
                var next = current + dir;
                Vector3i delta = next - centerIndex;

                // Bound check
                if (Math.Sqrt(delta.X * delta.X + delta.Y * delta.Y + delta.Z * delta.Z) < horizontalRadius &&
                    Math.Abs(delta.Y) <= verticalRadius &&
                    //ActiveChunks.TryGetValue(next, out var c)  &&
                    visited.Add(next)) // only enqueue if not seen
                {
                    //if(!c.IsEmpty)
                        queue.Enqueue(next);
                }
            }
        }
        
        return result;
    }

    // attempts to get a chunk from the list
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

    // self explanatory
    public Vector3i WorldPosToChunkIndex(Vector3i worldIndex, int chunkSize = Chunk.SIZE)
    {
        int chunkX = (int)Math.Floor(worldIndex.X / (double)chunkSize);
        int chunkY = (int)Math.Floor(worldIndex.Y / (double)chunkSize);
        int chunkZ = (int)Math.Floor(worldIndex.Z / (double)chunkSize);
        return new Vector3i(chunkX, chunkY, chunkZ);
    }

    public Vector3i WorldPosToChunkLocal(Vector3i worldIndex, int chunkSize = Chunk.SIZE)
    {
        Vector3i chunkIndex = WorldPosToChunkIndex(worldIndex, chunkSize);

        Vector3i localBlockPos = new Vector3i(
            worldIndex.X - chunkIndex.X * chunkSize,
            worldIndex.Y - chunkIndex.Y * chunkSize,
            worldIndex.Z - chunkIndex.Z * chunkSize
        );

        return localBlockPos;
    }

    // also self explanatory
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

    // translates the existing chunk dictionary intp just chunks list
    List<Chunk> ChunkList()
    {
        var result = new List<Chunk>();
        foreach (var chunk in ActiveChunks.Values)
            result.Add(chunk);

        return result;
    }
}
