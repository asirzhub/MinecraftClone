using Minecraft_Clone.Graphics;
using OpenTK.Mathematics;
using static Minecraft_Clone.Graphics.VBO;

namespace Minecraft_Clone.World.Chunks
{
    public class ChunkMesher
    {
        public Dictionary<Vector3i, MeshData> solidMeshes;
        public Dictionary<Vector3i, MeshData> liquidMeshes;

        public ChunkMesher()
        {
            solidMeshes = new Dictionary<Vector3i, MeshData>();
            liquidMeshes = new Dictionary<Vector3i, MeshData>();
        }

        public void GenerateMesh(Vector3i chunkIndex,
                                     ChunkWorld world)
        {
            // ONLY RE-MESH IF THIS CHUNK IS DIRTY!
            bool dirty = world.GetChunkAtIndex(chunkIndex, out var found).dirty;
            if (!dirty || !found)
                return;

            Vector3i localOrigin = chunkIndex * Chunk.SIZE;
            Chunk chunk = world.GetChunkAtIndex(chunkIndex, out bool thisChunkExists);
            Chunk forwardChunk = world.GetChunkAtIndex(chunkIndex + new Vector3i(0, 0, 1), out bool forwardChunkExists);
            Chunk rearChunk = world.GetChunkAtIndex(chunkIndex + new Vector3i(0, 0, -1), out bool rearChunkExists);
            Chunk leftChunk = world.GetChunkAtIndex(chunkIndex + new Vector3i(-1, 0, 0), out bool leftChunkExists);
            Chunk rightChunk = world.GetChunkAtIndex(chunkIndex + new Vector3i(+1, 0, 0), out bool rightChunkExists);
            Chunk topChunk = world.GetChunkAtIndex(chunkIndex + new Vector3i(0, +1, 0), out bool topChunkExists);
            Chunk belowChunk = world.GetChunkAtIndex(chunkIndex + new Vector3i(0, -1, 0), out bool belowChunkExists);

            MeshData solidResult = new MeshData();
            uint solidVertexOffset = 0;

            MeshData liquidResult = new MeshData();
            uint liquidVertexOffset = 0;

            // generates only the mesh for chunk with index chunkIndex
            // greedy mesh or face culling based on neighbors
            // use CubeMesh and blockData to generate vertex/index data
            for (byte x = 0; x < Chunk.SIZE; x++)
            {
                for (byte y = 0; y < Chunk.SIZE; y++)
                {
                    for (byte z = 0; z < Chunk.SIZE; z++)
                    {
                        Block block = chunk.GetBlock(x, y, z);
                        if (block.Type == BlockType.AIR) // skip air
                            continue;

                        //front: +z
                        //back: -z
                        //left: -x
                        //right: +x
                        //up: +y
                        //down: -y

                        Vector3i blockPos = new Vector3i(x, y, z);
                        Vector3i blockWorldPos = blockPos + chunkIndex * Chunk.SIZE;
                        bool[] occlusions = new bool[6];

                        // base-layer culling: hide faces hidden by solid blocks

                        // front
                        if (world.TryGetBlockAt(blockWorldPos + new Vector3i(0, 0, +1), out var bF))
                            occlusions[0] = bF.isSolid || (block.isWater && bF.isWater);

                        // back
                        if (world.TryGetBlockAt(blockWorldPos + new Vector3i(0, 0, -1), out var bB))
                            occlusions[1] = bB.isSolid || (block.isWater && bB.isWater); 

                        // left
                        if (world.TryGetBlockAt(blockWorldPos + new Vector3i(-1, 0, 0), out var bL))
                            occlusions[2] = bL.isSolid || (block.isWater && bL.isWater); 

                        // right
                        if (world.TryGetBlockAt(blockWorldPos + new Vector3i(+1, 0, 0), out var bR))
                            occlusions[3] = bR.isSolid || (block.isWater && bR.isWater);
                        
                        // up
                        if (world.TryGetBlockAt(blockWorldPos + new Vector3i(0, 1, 0), out var bU))
                            occlusions[4] = (bU.isSolid && !block.isWater && blockWorldPos.Y != world.seaLevel) || (block.isWater && bU.isWater);

                        // down
                        if (world.TryGetBlockAt(blockWorldPos + new Vector3i(0, -1, 0), out var bD))
                            occlusions[5] = bD.isSolid || (block.isWater && bD.isWater); 



                        // for each face
                        foreach (CubeMesh.Face face in Enum.GetValues(typeof(CubeMesh.Face)))
                        {
                            //if (face != CubeMesh.Face.TOP) continue;
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

                                if (blockWorldPos.Y < world.seaLevel && blockWorldPos.Y > world.seaLevel - 6)
                                {
                                    lightLevel = (byte)(15 - (world.seaLevel - blockWorldPos.Y));
                                }
                                else if (blockWorldPos.Y <= world.seaLevel - 6)
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

                                foreach (var direction in AOCheckDirection)
                                {
                                    world.TryGetBlockAt(blockWorldPos + direction, out var b);
                                    if (b.isSolid && lightLevel > 1)
                                    {
                                        lightLevel -= 1;
                                    }
                                }

                                if (block.isWater)
                                {
                                    liquidResult.Vertices.Add(
                                        new PackedVertex(lx, ly, lz, uv.X, uv.Y, normal, 15, true)
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

            UpdateSolidMeshList(chunkIndex, solidResult);
            UpdateLiquidMeshList(chunkIndex, liquidResult);

            world.GetChunkAtIndex(chunkIndex, out var _found).dirty = false;
        }


        void UpdateSolidMeshList(Vector3i index, MeshData mesh)
        {
            if (solidMeshes.ContainsKey(index))
            {
                solidMeshes[index] = mesh;
            }
            else
            {
                solidMeshes.Add(index, mesh);
            }
        }

        void UpdateLiquidMeshList(Vector3i index, MeshData mesh)
        {
            if (liquidMeshes.ContainsKey(index))
            {
                liquidMeshes[index] = mesh;
            }
            else
            {
                liquidMeshes.Add(index, mesh);
            }
        }

        public void DisposeMesh(Vector3i index)
        {
            if (solidMeshes.ContainsKey(index))
                solidMeshes[index].Dispose();
        }
    }
}