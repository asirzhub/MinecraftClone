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

    private Vector3i currentChunkIndex;

    public ChunkManager() { }

    public async Task UpdateAsync(Camera camera, float time, Vector3 sunDirection)
    {
        currentChunkIndex = WorldPosToChunkIndex(camera.position);

        var toLoad = loader.GetChunksToLoad(currentChunkIndex, camera, radius:4);
        world.GetUnloadList(toLoad, out var toUnload);

        foreach (var idx in toUnload) // nuke culled chunks immediately
        {
            mesher.DisposeMesh(idx);
            world.chunks.Remove(idx);
            genTasks.Remove(idx);
            meshTasks.Remove(idx);
        }

        foreach (var idx in toLoad)
        {
            if (!world.HasChunk(idx) && !genTasks.ContainsKey(idx))
            {
                // add an empty chunk to generate asyncly
                var chunk = world.AddNewChunk(idx, BlockType.AIR);
                var task = generator.GenerateChunkAsync(chunk, idx, world);
                genTasks[idx] = task;
            }
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
                meshTasks[idx] = mesher.GenerateMeshAsync(idx, world);
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

        // ▶︎ 6. Render whatever meshes are ready
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
