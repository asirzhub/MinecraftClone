using Minecraft_Clone;
using Minecraft_Clone.Graphics;
using Minecraft_Clone.World;
using Minecraft_Clone.World.Chunks;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using static Minecraft_Clone.Graphics.VBO;

public class ChunkManager
{
    // terminology:
    // active means that the chunk might need to immediately be render-ready, so it must be in ram
    // inactive is just null - the chunk has been removed from ram

    // Steps:
    // Check if any chunks need to be loaded.
    // Check if any chunks need to be unloaded, unload them and dispose their mesh.
    // Check if any chunks have been loaded, but need to be setup(voxel configuration, set active blocks, etc...).
    // Check if any chunks need to be rebuilt(i.e.a chunk was modified last frame and needs mesh rebuild).
    // Update the chunk visibility list(this is a list of all chunks that could potentially be rendered)

    // chunk state machine:
    //                                      marked dirty (block update)
    //                                       ,_____________________,
    //                                       V                     |
    // birth -> generating -> generated -> meshing -> meshed -> visible <--> invisible
    //                                                               (un)culled
    // 
    // all states can lead to death

    int seaLevel = 0;
    float noiseScale = 0.01f;

    public PerlinNoise noise = new PerlinNoise();

    // The list of all chunks that (could) be rendered, therefore should be readied
    List<Vector3i> ActivationList = new List<Vector3i>();
    List<Vector3i> LastActivations = new List<Vector3i>();
    Dictionary<Vector3i, Chunk> Chunks = new Dictionary<Vector3i, Chunk>();

    public ChunkRenderer renderer = new ChunkRenderer();

    int maxChunkTasks = 10;

    List<Chunk> ChunkList()
    {
        var result = new List<Chunk>();
        foreach (var chunk in Chunks.Values)
        {
            result.Add(chunk);
        }
        return result;
    }

    public async void Update(Camera camera, float time, Vector3 sunDirection)
    {
        int chunkTasks = 0;

        ActivationList = ListActiveChunks((0, 0, 0), 3, 3, 3, 1);

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
                Chunks.Add(idx, newChunk);
            }
            else
            {
                Chunk chunk = Chunks[idx];
                Console.WriteLine($"{idx}:{chunk.GetState().ToString()}");
                // depending on the state of each chunk, do something different
                switch (chunk.GetState())
                {
                    case ChunkState.BIRTH:
                        if(chunkTasks < maxChunkTasks)
                        {
                            chunkTasks++;
                            chunk.SetState(ChunkState.GENERATING);
                            await GenerateChunkAsync(chunk, idx, new CancellationToken());
                        }
                        break;
                    case ChunkState.GENERATING:
                        // wait until its done
                        break;
                    case ChunkState.GENERATED:
                        if (chunkTasks < maxChunkTasks)
                        {
                            chunkTasks++;
                            chunk.SetState(ChunkState.MESHING);
                            await GenerateMeshAsync(idx, ChunkList(), new CancellationToken());
                        }
                        break;
                    case ChunkState.MESHING:
                        //wait until its done
                        break;
                    case ChunkState.MESHED:
                        chunk.SetState(ChunkState.VISIBLE);
                        break;
                    case ChunkState.VISIBLE:
                        renderer.RenderChunk(Chunks[idx].solidMesh, camera, idx, time, sunDirection);// render the water after
                        break;
                    case ChunkState.INVISIBLE:
                        // dont call the draw calls
                        break;
                    case ChunkState.DIRTY:
                        // re-mesh it
                        break;
                }
            }
        }

        GL.DepthMask(false);
        foreach (var idx in ActivationList)
        {
            if (Chunks[idx].GetState() == ChunkState.VISIBLE)
                renderer.RenderChunk(Chunks[idx].liquidMesh, camera, idx, time, sunDirection);
        }
        GL.DepthMask(true);

        LastActivations = ActivationList;
    }

    public void DisposeChunk(Vector3i index)
    {
        Chunks[index].DisposeMeshes();
        Chunks.Remove(index);
    }

    public List<Vector3i> ListActiveChunks(Vector3i centerIndex, int horizontalRadius, int upRadius, int downRadius, int minY)
    {
        List<Vector3i> result = new List<Vector3i>();

        for (int x = -horizontalRadius; x < horizontalRadius; x++)
        {
            for (int z = -horizontalRadius; z < horizontalRadius; z++)
            {
                for (int y = -downRadius; y < upRadius; y++)
                {
                    result.Add(centerIndex + (x, y, z));
                }
            }
        }

        return result;
    }

    public bool TryGetChunkAtIndex(Vector3i index, out Chunk result)
    {
        result = default;
        if (Chunks.ContainsKey(index))
        {
            result = Chunks[index];
            return true;
        }
        return false;
    }

    public bool TryGetBlockAtWorldPosition(Vector3i worldIndex, out Block result)
    {
        result = default;
        int size = Chunk.SIZE;

        int chunkX = (int)Math.Floor(worldIndex.X / (double)size);
        int chunkY = (int)Math.Floor(worldIndex.Y / (double)size);
        int chunkZ = (int)Math.Floor(worldIndex.Z / (double)size);

        Vector3i chunkIndex = new Vector3i(chunkX, chunkY, chunkZ);

        Vector3i localBlockPos = new Vector3i(
            worldIndex.X - chunkX * size,
            worldIndex.Y - chunkY * size,
            worldIndex.Z - chunkZ * size
        );

        // sanity check (should not be necessary if floor division is correct)
        if (localBlockPos.X < 0 || localBlockPos.X >= size ||
            localBlockPos.Y < 0 || localBlockPos.Y >= size ||
            localBlockPos.Z < 0 || localBlockPos.Z >= size)
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

    public async Task GenerateChunkAsync(Chunk chunk, Vector3i chunkIndex, CancellationToken token)
    {
        await Task.Run(() =>
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

                        chunk.SetBlock(x, y, z, type);
                        if (type != BlockType.AIR) totalBlocks++;
                    }
                }
            }

            chunk.SetState(ChunkState.GENERATED);
        });
    }

    public Task<bool> GenerateMeshAsync(Vector3i chunkIndex, List<Chunk> chunks, CancellationToken token)
    {
        
        // no remeshing visible chunks
        var thisChunkExists = TryGetChunkAtIndex(chunkIndex, out var chunk);

        if(thisChunkExists && chunk.GetState() == ChunkState.VISIBLE) return Task.FromResult(false);


        return Task.Run(() =>
        {

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
            chunk.solidMesh = solidResult;
            chunk.liquidMesh = liquidResult;

            chunk.SetState(ChunkState.MESHED);
            return true;
        });
    }
}
