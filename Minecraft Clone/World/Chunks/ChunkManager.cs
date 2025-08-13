using Minecraft_Clone;
using Minecraft_Clone.Graphics;
using Minecraft_Clone.World;
using Minecraft_Clone.World.Chunks;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using System.Collections.Concurrent;
using System;
using static Minecraft_Clone.Graphics.VBO;
using System.Reflection.Metadata.Ecma335;

public class ChunkManager
{
    static int seaLevel = 0;
    static float noiseScale = 0.01f;
    static int maxChunkTasks = 8;

    public PerlinNoise noise = new PerlinNoise();

    // The list of all chunks that (could) be rendered, therefore should be readied
    List<Vector3i> ActivationList = new List<Vector3i>();
    List<Vector3i> LastActivations = new List<Vector3i>();
    ConcurrentDictionary<Vector3i, Chunk> Chunks = new ConcurrentDictionary<Vector3i, Chunk>();

    public class CompletedMesh
    {
        public Vector3i Index;
        public MeshData SolidMesh;
        public MeshData LiquidMesh;

        public CompletedMesh(Vector3i index, MeshData solidMesh, MeshData liquidMesh)
        {
            this.Index = index;
            this.SolidMesh = solidMesh;
            this.LiquidMesh = liquidMesh;
        }

        public void Dispose() {
            this.SolidMesh.Dispose();
            this.LiquidMesh.Dispose();            
        }
    };

    ConcurrentQueue<CompletedMesh> completedMeshes = new();
    ConcurrentDictionary<Vector3i, CancellationTokenSource> ChunkCancelTokens = new();
    SemaphoreSlim MeshSemaphore = new(maxChunkTasks);

    public class CompletedChunk
    {
        public Vector3i Index;
        public Block[] Blocks;

        public CompletedChunk(Vector3i index, Block[] blocks)
        {
            this.Index = index;
            this.Blocks = blocks;
        }

        public void Dispose() {
            Array.Clear(this.Blocks);
        }
    }

    ConcurrentQueue<CompletedChunk> CompletedChunkBlocks = new();
    SemaphoreSlim ChunkGenSemaphore = new(maxChunkTasks);

    public ChunkRenderer renderer = new ChunkRenderer();

    List<Chunk> ChunkList()
    {
        var result = new List<Chunk>();
        foreach (var chunk in Chunks.Values)
        {
            result.Add(chunk);
        }
        return result;
    }

    public Vector3i currentChunk = new();

    public void Update(Camera camera, float time, Vector3 sunDirection)
    {
        ActivationList = ListActiveChunks(camera.position, 4, 3, 1);

        currentChunk = WorldPosToChunkIndex(Vec3ToVec3i(camera.position));

        // move chunk block data if there's any ready
        while (CompletedChunkBlocks.TryDequeue(out var cb))
        {
            // chunk was removed at some point
            if (!Chunks.TryGetValue(cb.Index, out var chunk))
            {
                cb.Dispose();
                continue;
            }
            // or if it was cancelled
            if (ChunkCancelTokens.TryGetValue(cb.Index, out var tokenSource) && tokenSource.IsCancellationRequested)
            {
                cb.Dispose();                
                continue;
            }

            chunk.blocks = cb.Blocks;
            chunk.SetState(ChunkState.GENERATED);
        }

        // move chunk mesh data around as it is readied
        while (completedMeshes.TryDequeue(out var cm))
        {
            // chunk was removed at some point
            if (!Chunks.TryGetValue(cm.Index, out var chunk))
            {
                cm.Dispose();
                continue;
            }

            // if the chunk can't make the valid state transition, skip
            if (chunk.GetState() == ChunkState.BIRTH) continue;

            // or if the chunk has been canceled
            if (ChunkCancelTokens.TryGetValue(cm.Index, out var tokenSource) && tokenSource.IsCancellationRequested)
            {
                cm.Dispose();
                continue;
            }

            chunk.DisposeMeshes(); // clear gpu data

            chunk.solidMesh = cm.SolidMesh;
            chunk.liquidMesh = cm.LiquidMesh;

            chunk.solidMesh.Upload();
            chunk.liquidMesh.Upload();

            chunk.SetState(ChunkState.MESHED);
        }

        // deactivate chunks that are not in the list of active chunks
        foreach (var idx in LastActivations)
        {
            if (!ActivationList.Contains(idx))
            {
                DisposeChunk(idx);
            }
        }

        // for every active chunk
        foreach (var idx in ActivationList)
        {
            // new chunk wasnt active before must be added to the list
            if (!Chunks.ContainsKey(idx))
            {
                Chunk newChunk = new Chunk();
                Chunks.TryAdd(idx, newChunk);
            }
            else
            {
                Chunk chunk = Chunks[idx];
                var state = chunk.GetState();

                // manage culling here
                //if (state == ChunkState.VISIBLE || state == ChunkState.INVISIBLE)
                //    chunk.SetState(IsChunkVisible(idx, time, camera) ? ChunkState.VISIBLE : ChunkState.INVISIBLE);

                //Console.WriteLine($"{idx}:{chunk.GetState().ToString()}");
                // depending on the state of each chunk, do something different
                switch (chunk.GetState())
                {
                    case ChunkState.BIRTH:
                        StartGenerationJob(idx);
                        break;
                    case ChunkState.GENERATING:
                        break;
                    case ChunkState.GENERATED:
                        StartMeshJob(idx);
                        break;
                    case ChunkState.MESHING:
                        //wait until its done
                        break;
                    case ChunkState.MESHED:
                        // HERE
                        chunk.SetState(ChunkState.VISIBLE);
                        break;
                    case ChunkState.VISIBLE:
                        renderer.RenderChunk(Chunks[idx].solidMesh, camera, idx, time, sunDirection);// render the water after
                        break;
                    case ChunkState.INVISIBLE:
                        // dont call the draw calls
                        break;
                    case ChunkState.DIRTY:
                        StartMeshJob(idx, isRebuild: true);
                        break;
                }
            }
        }

        GL.DepthMask(false);
        foreach (var idx in ActivationList)
        {
            var safe = TryGetChunkAtIndex(idx, out var chunk);
            if (safe && chunk.GetState() == ChunkState.VISIBLE)
                renderer.RenderChunk(chunk.liquidMesh, camera, idx, time, sunDirection);
        }
        GL.DepthMask(true);

        LastActivations = new List<Vector3i>(ActivationList);
    }

    public static Vector3 ProjectVector(Vector3 u, Vector3 v, out bool backward)
    {
        // Calculate the dot product of u and v
        float dotProduct = Vector3.Dot(u, v);

        backward = dotProduct < 0;

        // Calculate the squared magnitude of v
        float vSquaredMagnitude = v.LengthSquared; // Or v.Length * v.Length;

        // Calculate the scalar multiple
        float scalar = dotProduct / vSquaredMagnitude;

        // Multiply the scalar by vector v to get the projection
        return v * scalar;
    }

    public Vector3i Vec3ToVec3i(Vector3 input) {
        var result = new Vector3i();
        result.X = (int)MathF.Round(input.X);
        result.Y = (int)MathF.Round(input.Y);
        result.Z = (int)MathF.Round(input.Z);
        return result;
    }

    void StartMeshJob(Vector3i index, bool isRebuild = false)
    {
        if (!Chunks.TryGetValue(index, out Chunk chunk)) return;

        if (isRebuild)
        {
            List<Vector3i> neighborVectors = new List<Vector3i>() {
                                            Vector3i.UnitX, -Vector3i.UnitX,
                                            Vector3i.UnitY, -Vector3i.UnitY,
                                            Vector3i.UnitZ, -Vector3i.UnitZ,};

            // mark neighbor chunks dirty
            foreach (var v in neighborVectors)
            {
                if (Chunks.TryGetValue(index + v, out var neighbor))
                    neighbor.TryMarkDirty();
            }
        }

        // Avoid starting another mesh if already meshing (optional)
        if (chunk.GetState() == ChunkState.MESHING) return;

        chunk.SetState(ChunkState.MESHING);

        var cts = new CancellationTokenSource();
        ChunkCancelTokens[index] = cts;

        // start background worker (no GL calls here, no SetState here)
        _ = Task.Run(async () =>
        {
            await MeshSemaphore.WaitAsync(cts.Token);
            try
            {
                var completed = await BuildMesh(index, ChunkList(), cts.Token);
                completedMeshes.Enqueue(completed);
            }
            catch (OperationCanceledException) { /* canceled */ }
            catch (Exception ex)
            {
                Console.WriteLine($"Mesh build failed for {index}: {ex}");
            }
            finally
            {
                MeshSemaphore.Release();
            }
        }, cts.Token);
    }

    void StartGenerationJob(Vector3i index)
    {
        if (!Chunks.TryGetValue(index, out Chunk chunk)) return;

        // Avoid starting another mesh if already meshing 
        if (chunk.GetState() == ChunkState.MESHING) return;

        chunk.SetState(ChunkState.GENERATING);

        var cts = new CancellationTokenSource();
        ChunkCancelTokens[index] = cts;

        // start background worker (no GL calls here, no SetState here)
        _ = Task.Run(async () =>
        {
            await ChunkGenSemaphore.WaitAsync(cts.Token);
            try
            {
                // Await BuildMesh instead of .Result so we don't block the thread pool
                var completed = await BuildBlocks(index, cts.Token);
                // Enqueue the CPU-side mesh for the main thread to apply
                CompletedChunkBlocks.Enqueue(completed);
            }
            catch (OperationCanceledException) { /* canceled */ }
            catch (Exception ex)
            {
                Console.WriteLine($"Block generation failed for {index}: {ex}");
            }
            finally
            {
                ChunkGenSemaphore.Release();
            }
        }, cts.Token);

    }

    public void DisposeChunk(Vector3i index)
    {
        Chunks[index].DisposeMeshes();
        Chunks.TryRemove(index, out var val);
    }

    public List<Vector3i> ListActiveChunks(Vector3 centerWorldPos, int horizontalRadius, int verticalRadius, int minY)
    {
        List<Vector3i> result = new();

        Vector3i centerIndex = WorldPosToChunkIndex((
                                (int)MathF.Floor(centerWorldPos.X),
                                (int)MathF.Floor(centerWorldPos.Y),
                                (int)MathF.Floor(centerWorldPos.Z)));
        result.Add(centerIndex);

        List<int> yValues = new() { centerIndex.Y };
        for(int i = 1; i <= verticalRadius; i++)
        {
            yValues.Add(centerIndex.Y + i);
            yValues.Add(centerIndex.Y - i);
            result.Add(centerIndex + i * Vector3i.UnitY);
            result.Add(centerIndex - i * Vector3i.UnitY);
        }

        foreach(var y in yValues)
        {
            for(int r = 0; r <= horizontalRadius; r++)
            {
                for(int h = -(r); h < r; h++)
            {
                result.Add(centerIndex + r * Vector3i.UnitX + h * Vector3i.UnitZ + y * Vector3i.UnitY);
                result.Add(centerIndex + r * -Vector3i.UnitZ + h * Vector3i.UnitX + y * Vector3i.UnitY);
                result.Add(centerIndex + r * -Vector3i.UnitX + h * -Vector3i.UnitZ + y * Vector3i.UnitY);
                result.Add(centerIndex + r * Vector3i.UnitZ + h * -Vector3i.UnitX  + y * Vector3i.UnitY);
            }
            }
        }

        return result;
    }

    public bool TryGetChunkAtIndex(Vector3i index, out Chunk result)
    {
        result = default;

        var exists = Chunks.TryGetValue(index, out var chunk);

        if (exists)
        {
            result = chunk;
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

    public Task<CompletedChunk> BuildBlocks(Vector3i chunkIndex, CancellationToken token)
    {
        var tempChunk = new Chunk();

        return Task.Run(() =>
        {
            //watch.Start();
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
                        BlockType type = BlockType.AIR;

                        if (worldY < seaLevel)
                        {
                            type = BlockType.AIR;
                        }

                        var landBias = 3 * Chunk.SIZE * (noise.Noise((float)worldX * noiseScale, (float)worldZ * noiseScale) - 0.5);

                        if (worldY <= seaLevel)
                            type = BlockType.WATER;

                        if (worldY < landBias && worldY > -64)
                            type = BlockType.STONE;

                        if (worldY < -50)
                            type = BlockType.AIR;

                        tempChunk.SetBlock(x, y, z, type);
                        if (type != BlockType.AIR) totalBlocks++;
                    }
                }
            }
            return new CompletedChunk(chunkIndex, tempChunk.blocks);
        });
    }

    public Task<CompletedMesh> BuildMesh(Vector3i chunkIndex, List<Chunk> chunks, CancellationToken token)
    {
        // no remeshing visible chunks
        bool thisChunkExists = TryGetChunkAtIndex(chunkIndex, out var chunk);

        // just keep waiting until the chunk exists
        while (!thisChunkExists)
        {
            thisChunkExists = TryGetChunkAtIndex(chunkIndex, out chunk);
        }

        return Task.Run(async () =>
        {
            await Task.Delay(2);

            Vector3i localOrigin = chunkIndex * Chunk.SIZE;
            bool forwardChunkExists = TryGetChunkAtIndex(chunkIndex + new Vector3i(0, 0, 1), out Chunk forwardChunk);
            bool rearChunkExists = TryGetChunkAtIndex(chunkIndex + new Vector3i(0, 0, -1), out Chunk rearChunk);
            bool leftChunkExists = TryGetChunkAtIndex(chunkIndex + new Vector3i(-1, 0, 0), out Chunk leftChunk);
            bool rightChunkExists = TryGetChunkAtIndex(chunkIndex + new Vector3i(+1, 0, 0), out Chunk rightChunk);
            bool topChunkExists = TryGetChunkAtIndex(chunkIndex + new Vector3i(0, +1, 0), out Chunk topChunk);
            bool belowChunkExists = TryGetChunkAtIndex(chunkIndex + new Vector3i(0, -1, 0), out Chunk belowChunk);

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
                            occlusions[4] = (bU.isSolid && !block.isWater && blockWorldPos.Y != seaLevel) || (block.isWater && bU.isWater);

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
}
