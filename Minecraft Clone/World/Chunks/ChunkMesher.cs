using Minecraft_Clone.Graphics;
using OpenTK.Mathematics;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Minecraft_Clone.Graphics.VBO;
using static Minecraft_Clone.World.Chunks.ChunkGenerator;

namespace Minecraft_Clone.World.Chunks
{
    public class ChunkMesher
    {
        public ChunkMesher()
        {

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
        public Task MeshTask(Vector3i index, ChunkManager manager, 
            CancellationTokenSource cts, ConcurrentQueue<CompletedMesh> queue, 
            ConcurrentDictionary<Vector3i, Chunk> ActiveChunks, List<Chunk> chunkList,
            byte LOD = 1)
        {
            Chunk thisChunk = ActiveChunks[index];

            // skip meshing if it's empty
            if (thisChunk.IsEmpty)
            {
                CompletedMesh result = new CompletedMesh(index, new MeshData(), new MeshData());
                queue.Enqueue(result);
                return Task.CompletedTask;
            }

            return Task.Run(async () =>
            {

                var result = await BuildMesh(index, manager, chunkList, cts.Token, LOD);

                queue.Enqueue(result);

                //RunningTasks.TryRemove(index, out _);
                //RunningTasksCTS.TryRemove(index, out _);
            });
        }

        Task<CompletedMesh> BuildMesh(Vector3i chunkIndex, ChunkManager manager, List<Chunk> chunks, CancellationToken token, byte LOD)
        {
            // no remeshing visible chunks
            bool thisChunkExists = manager.TryGetChunkAtIndex(chunkIndex, out var chunk);
            return Task.Run(async () =>
            {
                Vector3i localOrigin = chunkIndex * Chunk.SIZE;
                bool forwardChunkExists = manager.TryGetChunkAtIndex(chunkIndex + new Vector3i(0, 0, 1), out Chunk forwardChunk);
                bool rearChunkExists = manager.TryGetChunkAtIndex(chunkIndex + new Vector3i(0, 0, -1), out Chunk rearChunk);
                bool leftChunkExists = manager.TryGetChunkAtIndex(chunkIndex + new Vector3i(-1, 0, 0), out Chunk leftChunk);
                bool rightChunkExists = manager.TryGetChunkAtIndex(chunkIndex + new Vector3i(+1, 0, 0), out Chunk rightChunk);
                bool topChunkExists = manager.TryGetChunkAtIndex(chunkIndex + new Vector3i(0, +1, 0), out Chunk topChunk);
                bool belowChunkExists = manager.TryGetChunkAtIndex(chunkIndex + new Vector3i(0, -1, 0), out Chunk belowChunk);

                int seaLevel = manager.worldGenerator.seaLevel;

                MeshData solidResult = new MeshData();
                uint solidVertexOffset = 0;

                MeshData liquidResult = new MeshData();
                uint liquidVertexOffset = 0;

                // local chunk coordinate 
                for (byte x = 0; x < Chunk.SIZE; x+=LOD)
                {
                    for (byte y = 0; y < Chunk.SIZE; y+=LOD)
                    {
                        for (byte z = 0; z < Chunk.SIZE; z+=LOD)
                        {
                            token.ThrowIfCancellationRequested();

                            Block block = chunk.GetBlock(x, y, z);
                            if (block.Type == BlockType.AIR) // skip air
                                continue;

                            Vector3i blockWorldPos = localOrigin + (x, y, z);

                            // for each face
                            if (block.Type != BlockType.TALLGRASS)
                            {
                                // base-layer culling: hide faces hidden by solid blocks
                                bool[] occlusions = new bool[6];

                                // front
                                if (manager.TryGetBlockAtWorldPosition(blockWorldPos + new Vector3i(0, 0, +LOD), out var bF))
                                    occlusions[0] = bF.isSolid || (block.isWater && bF.isWater);

                                // back
                                if (manager.TryGetBlockAtWorldPosition(blockWorldPos + new Vector3i(0, 0, -LOD), out var bB))
                                    occlusions[1] = bB.isSolid || (block.isWater && bB.isWater);

                                // left
                                if (manager.TryGetBlockAtWorldPosition(blockWorldPos + new Vector3i(-LOD, 0, 0), out var bL))
                                    occlusions[2] = bL.isSolid || (block.isWater && bL.isWater);

                                // right
                                if (manager.TryGetBlockAtWorldPosition(blockWorldPos + new Vector3i(+LOD, 0, 0), out var bR))
                                    occlusions[3] = bR.isSolid || (block.isWater && bR.isWater);

                                // up
                                if (manager.TryGetBlockAtWorldPosition(blockWorldPos + new Vector3i(0, +LOD, 0), out var bU))
                                    occlusions[4] = bU.isSolid || (block.isWater && bU.isWater); ;

                                // down
                                if (manager.TryGetBlockAtWorldPosition(blockWorldPos + new Vector3i(0, -LOD, 0), out var bD))
                                    occlusions[5] = bD.isSolid || (block.isWater && bD.isWater);

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
                                        byte lx = (byte)(x + vPos.X * LOD);
                                        byte ly = (byte)(y + vPos.Y * LOD);
                                        byte lz = (byte)(z + vPos.Z * LOD);

                                        // Compute UV
                                        var registryData = BlockRegistry.Types[block.Type];
                                        var faceUVs = registryData.FaceUVs;
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
                                                manager.TryGetBlockAtWorldPosition(blockWorldPos + direction, out var b);
                                                if (b.isSolid)
                                                {
                                                    occluderCount += (byte)1;
                                                }
                                            }
                                        }

                                        if (occluderCount >= 2) lightLevel -= occluderCount;//ambient occlusion can only happen with two or more occluders

                                        if (block.isWater)
                                        {
                                            liquidResult.Vertices.Add(
                                                new PackedVertex(lx, ly, lz, uv.X, uv.Y, normal, lightLevel, registryData.wiggleType)
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
                                                new PackedVertex(lx, ly, lz, uv.X, uv.Y, normal, lightLevel, registryData.wiggleType)
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
                            else if (block.Type == BlockType.TALLGRASS)
                            {
                                foreach (var face in GrassMesh.packedVertices)
                                {
                                    var faceUVs = BlockRegistry.Types[block.Type].FaceUVs;
                                    Vector2 tile = faceUVs[0];

                                    foreach (var vertex in face)
                                    {
                                        Vector3i vPos = vertex.Position();
                                        byte lx = (byte)(x + vPos.X * LOD);
                                        byte ly = (byte)(y + vPos.Y * LOD);
                                        byte lz = (byte)(z + vPos.Z * LOD);

                                        byte lightLevel = 15;

                                        Vector2 uv = (tile + (vertex.TexU, vertex.TexV)) / 8f;
                                        uv.Y = 1f - uv.Y;

                                        // 4 - normal pointed upward, matching the surface it's on
                                        var wiggleType = BlockRegistry.Types[block.Type].wiggleType;
                                        if (vPos.Y == 0) wiggleType = WiggleType.NONE;
                                        solidResult.Vertices.Add(
                                                new PackedVertex(lx, ly, lz, uv.X, uv.Y, 4, lightLevel, wiggleType)
                                            );

                                        // Two triangles (0,1,2) & (2,3,0)
                                        solidResult.Indices.Add(solidVertexOffset + 0);
                                        solidResult.Indices.Add(solidVertexOffset + 1);
                                        solidResult.Indices.Add(solidVertexOffset + 2);
                                        solidResult.Indices.Add(solidVertexOffset + 2);
                                        solidResult.Indices.Add(solidVertexOffset + 3);
                                        solidResult.Indices.Add(solidVertexOffset + 0);

                                        //solidVertexOffset += 4;
                                    }
                                }
                            }


                        }
                    }
                }
                return new CompletedMesh(chunkIndex, solidResult, liquidResult);
            });
        }

        //===============================================================================================
    }
}
