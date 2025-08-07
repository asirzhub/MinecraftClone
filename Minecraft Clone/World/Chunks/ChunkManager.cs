using Minecraft_Clone;
using Minecraft_Clone.World;
using Minecraft_Clone.World.Chunks;
using OpenTK.Mathematics;
using OpenTK.Graphics.OpenGL4;
using Minecraft_Clone.Graphics;

public class ChunkManager
{
    private ChunkLoader loader = new ChunkLoader();
    private ChunkWorld world = new ChunkWorld();
    private ChunkGenerator generator = new ChunkGenerator();
    private ChunkMesher mesher = new ChunkMesher();
    private ChunkRenderer renderer = new ChunkRenderer();

    // Track in-flight tasks
    private Dictionary<Vector3i, Task> genTasks = new();
    private Dictionary<Vector3i, Task<bool>> meshTasks = new();
    private Dictionary<Vector3i, CancellationTokenSource> cancelTokens = new();

    private Vector3i currentChunkIndex;

    public ChunkManager() { }

    public async Task UpdateAsync(Camera camera, float time, Vector3 sunDirection)
    {
        //steps
        // 1. get a list of chunks to load
        // 2. based on that, generate a list of chunks to unload
        // 4. for every chunk that needs to be loaded
        // 5. if the world doesnt have it, and there is not already a generator task, make a new task for that chunk
        // 6. for every task in the generation tasks that's completed, add it to the list of finished generations
        // 7. for every finished generation, check if it's been meshed - if not, start a mesh creation task
        // 8. for every completed mesh task, remove it from the task dictionary
        // 9. for every chunk that needs to be unloaded, unload it
        // 10. render every in the mesher

        currentChunkIndex = WorldPosToChunkIndex(camera.position);

        var toLoad = loader.GetChunksToLoad(currentChunkIndex, camera, radius:2);
        world.GetUnloadList(toLoad, out var toUnload);

        foreach (var idx in toLoad)
        {
            if (!world.HasChunk(idx) && !genTasks.ContainsKey(idx))
            {
                // add an empty chunk to generate asyncly
                var chunk = world.AddNewChunk(idx, BlockType.AIR);

                var cts = new CancellationTokenSource();
                cancelTokens[idx] = cts;
                genTasks[idx] = generator.GenerateChunkAsync(chunk, idx, world, cts.Token);            }
        }

        List<Vector3i> finishedGen = new();
        foreach (var idx in genTasks)
        {
            if (idx.Value.IsCompletedSuccessfully)
            {
                finishedGen.Add(idx.Key);
            }
        }

        foreach (var idx in finishedGen)
        {
            // avoid re-meshing if already meshing
            if (!meshTasks.ContainsKey(idx))
            {
                var cts = new CancellationTokenSource();
                cancelTokens[idx] = cts;
                meshTasks[idx] = mesher.GenerateMeshAsync(idx, world, cts.Token);
            }
            genTasks.Remove(idx);
        }

        List<Vector3i> finishedMesh = new();

        foreach (var kvp in meshTasks)
        {
            if(kvp.Value.IsCompletedSuccessfully && kvp.Value.Result)
            {
                finishedMesh.Add(kvp.Key);
            }
        }

        foreach (var idx in finishedMesh)
        {
            meshTasks.Remove(idx);
        }

        foreach (var idx in toUnload) // nuke culled chunks
        {
            if (cancelTokens.TryGetValue(idx, out var cts))
            {
                cts.Cancel();
                cts.Dispose();
                cancelTokens.Remove(idx);
            }
            mesher.DisposeMesh(idx);
            world.chunks.Remove(idx);
            genTasks.Remove(idx);
            meshTasks.Remove(idx);
        }

        // Render whatever meshes are ready
        Render(camera, time, sunDirection);
    }

    private void Render(Camera camera, float time, Vector3 sunDirection)
    {
        foreach (var (idx, mesh) in mesher.solidMeshes)
            renderer.RenderChunk(mesh, camera, idx, time, sunDirection);

        GL.DepthMask(false);
        foreach (var (idx, mesh) in mesher.liquidMeshes)
            renderer.RenderChunk(mesh, camera, idx, time, sunDirection);
        GL.DepthMask(true);
    }



    private Vector3i WorldPosToChunkIndex(Vector3 pos)
        => new Vector3i((int)MathF.Floor(pos.X / Chunk.SIZE),
                        (int)MathF.Floor(pos.Y / Chunk.SIZE),
                        (int)MathF.Floor(pos.Z / Chunk.SIZE));
}
