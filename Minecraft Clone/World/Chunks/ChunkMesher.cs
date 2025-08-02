using Minecraft_Clone.Graphics;
using OpenTK.Mathematics;
using static Minecraft_Clone.Graphics.VBO;

namespace Minecraft_Clone.World.Chunks
{
    public class ChunkMesher
    {
        public Dictionary<Vector3i, MeshData> meshes;

        public ChunkMesher()
        {
            meshes = new Dictionary<Vector3i, MeshData>();
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

            MeshData result = new MeshData();
            uint vertexOffset = 0;

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
                            occlusions[1] = bB.isSolid || (block.isWater && bB.isWater); ;

                        // left
                        if (world.TryGetBlockAt(blockWorldPos + new Vector3i(-1, 0, 0), out var bL))
                            occlusions[2] = bL.isSolid || (block.isWater && bL.isWater); ;

                        // right
                        if (world.TryGetBlockAt(blockWorldPos + new Vector3i(+1, 0, 0), out var bR))
                            occlusions[3] = bR.isSolid || (block.isWater && bR.isWater); ;

                        // down
                        if (world.TryGetBlockAt(blockWorldPos + new Vector3i(0, 1, 0), out var bD))
                            occlusions[4] = bD.isSolid || (block.isWater && bD.isWater); ;

                        // up
                        if (world.TryGetBlockAt(blockWorldPos + new Vector3i(0, -1, 0), out var bU))
                            occlusions[5] = bU.isSolid || (block.isWater && bU.isWater); ;

                        // next step: water culling, only topmostlayer visible

                        // for each face
                        foreach (CubeMesh.Face face in Enum.GetValues(typeof(CubeMesh.Face)))
                        {
                            int fi = (int)face;
                            if (occlusions[fi]) continue;              // skip hidden faces
                            var faceVerts = CubeMesh.PackedFaceVertices[face];

                            // Append 4 corners
                            for (int i = 0; i < 4; i++)
                            {
                                var v = faceVerts[i];

                                // Local position within chunk (0..16)
                                var vPos = v.Position();
                                byte lx = (byte) (x + vPos.X);
                                byte ly = (byte) (y + vPos.Y);
                                byte lz = (byte) (z + vPos.Z);

                                // Compute UV
                                var faceUVs = BlockRegistry.Types[block.Type].FaceUVs;
                                Vector2 tile = faceUVs[fi];
                                Vector2 uv = (tile + (v.TexU, v.TexV)) / 8f;
                                uv.Y = 1f - uv.Y;

                                // Normal index 0..5
                                byte normal = (byte)face;

                                byte redLight = 15;
                                byte greenLight = 15;
                                byte blueLight = 15;
                                if(blockWorldPos.Y < world.seaLevel)
                                {
                                    redLight -= (byte)MathF.Min(-blockWorldPos.Y * 2, 15);
                                    greenLight -= (byte)MathF.Min(-blockWorldPos.Y, 15);
                                }

                                result.Vertices.Add(
                                    new PackedVertex(lx, ly, lz, uv.X, uv.Y, normal, redLight, greenLight, blueLight)
                                );


                                // Two triangles (0,1,2) & (2,3,0)
                                result.Indices.Add(vertexOffset + 0);
                                result.Indices.Add(vertexOffset + 1);
                                result.Indices.Add(vertexOffset + 2);
                                result.Indices.Add(vertexOffset + 2);
                                result.Indices.Add(vertexOffset + 3);
                                result.Indices.Add(vertexOffset + 0);

                                vertexOffset += 4;

                                if (x == Chunk.SIZE - 1 || y == Chunk.SIZE - 1 || z == Chunk.SIZE - 1) 
                                    continue;
                            }
                        }
                    }
                }
            }

            UpdateMeshList(chunkIndex, result);

            world.GetChunkAtIndex(chunkIndex, out var _found).dirty = false;
        }


        void UpdateMeshList(Vector3i index, MeshData mesh)
        {
            if (meshes.ContainsKey(index))
            {
                meshes[index] = mesh;
            }
            else
            {
                meshes.Add(index, mesh);
            }

        }

        public void DisposeMesh(Vector3i index)
        {
            if (meshes.ContainsKey(index))
                meshes[index].Dispose();
        }
    }
}