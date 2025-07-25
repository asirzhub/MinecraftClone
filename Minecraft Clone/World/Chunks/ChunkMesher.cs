using Minecraft_Clone.Graphics;
using OpenTK.Mathematics;

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
            Vector3i localOrigin = chunkIndex * Chunk.SIZE;
            Chunk chunk = world.GetChunkAtIndex(chunkIndex, out bool thisChunkExists); 
            Chunk forwardChunk = world.GetChunkAtIndex(chunkIndex + new Vector3i(0, 0, -1), out bool forwardChunkExists);
            Chunk rearChunk = world.GetChunkAtIndex(chunkIndex + new Vector3i(0, 0, +1), out bool rearChunkExists);
            Chunk leftChunk = world.GetChunkAtIndex(chunkIndex + new Vector3i(-1, 0, 0), out bool leftChunkExists);
            Chunk rightChunk = world.GetChunkAtIndex(chunkIndex + new Vector3i(+1, 0, 0), out bool rightChunkExists);
            Chunk topChunk = world.GetChunkAtIndex(chunkIndex + new Vector3i(0, +1, 0), out bool topChunkExists);
            Chunk belowChunk = world.GetChunkAtIndex(chunkIndex + new Vector3i(0, -1, 0), out bool belowChunkExists);


            MeshData result = new MeshData();
            uint vertexOffset = 0;

            // generates only the mesh for chunk with index chunkIndex
            // greedy mesh or face culling based on neighbors
            // use CubeMesh and blockData to generate vertex/index data
            for (int x = 0; x < Chunk.SIZE; x++)
            {
                for (int y = 0; y < Chunk.SIZE; y++)
                {
                    for (int z = 0; z < Chunk.SIZE; z++)
                    {
                        Block block = chunk.GetBlock(x, y, z);
                        if (block.Type == BlockType.AIR) // skip air
                            continue;

                        //check in all six directions of this block
                        bool[] occlusions = new bool[6];

                        // if we're at the front edge of chunk, and the next chunk over exists, and there is a block there
                        // or if the front block exists
                        // occlude the front side of the block
                        // otherwise, show it.

                        // forward/front
                        if (z == 0 && forwardChunkExists && forwardChunk.GetBlock(x, y, Chunk.SIZE - 1).isSolid
                            || z!= 0 && chunk.GetBlock(x, y, z - 1).isSolid)
                            occlusions[0] = true;
                        else
                            occlusions[0] = false;

                        // rear/back
                        if (z == Chunk.SIZE - 1 && rearChunkExists && rearChunk.GetBlock(x, y, 0).isSolid
                            || z != Chunk.SIZE - 1 && chunk.GetBlock(x, y, z + 1).isSolid)
                            occlusions[1] = true;
                        else
                            occlusions[1] = false;

                        //left
                        if (x == 0 && leftChunkExists && leftChunk.GetBlock(Chunk.SIZE - 1, y, z).isSolid
                            || x != 0 && chunk.GetBlock(x - 1, y, z).isSolid)
                            occlusions[2] = true;
                        else
                            occlusions[2] = false;

                        // right
                        if (x == Chunk.SIZE - 1 && rightChunkExists && rightChunk.GetBlock(0, y, z).isSolid
                            || x != Chunk.SIZE - 1 &&  chunk.GetBlock(x + 1, y, z).isSolid)
                            occlusions[3] = true;
                        else
                            occlusions[3] = false;

                        // above
                        if (y == Chunk.SIZE - 1 && topChunkExists && topChunk.GetBlock(x, 0, z).isSolid
                            || y != Chunk.SIZE - 1 && chunk.GetBlock(x, y + 1, z).isSolid)
                            occlusions[5] = true;
                        else
                            occlusions[5] = false;

                        // below
                        if (y == 0 && belowChunkExists && belowChunk.GetBlock(x, Chunk.SIZE - 1, z).isSolid
                            || y!= 0 &&  chunk.GetBlock(x, y - 1, z).isSolid)
                            occlusions[4] = true;
                        else
                            occlusions[4] = false;

                        // for each face of the block
                        foreach (CubeMesh.Face face in Enum.GetValues(typeof(CubeMesh.Face)))
                        {
                            int faceIndex = (int)face;
                            if (occlusions[faceIndex]) continue; // skip the vertices if its invisible

                            var faceVerts = CubeMesh.FaceVertices[face];

                            for (int i = 0; i < 4; i++) // four corners
                            {
                                var v = faceVerts[i];
                                var faceUVs = BlockRegistry.Types[block.Type].FaceUVs;
                                Vector2 tile = faceUVs[faceIndex];
                                tile += v.TexCoord; // apply the corner offset
                                Vector2 uvCoord = tile / 8f; // scale down to uv coordinates
                                uvCoord.Y = 1.0f - uvCoord.Y; // flip y because stbimagesharp trolls you hard

                                Vector3i blockPos = new Vector3i(x, y, z);
                                Vector3i blockWorldPos = blockPos + chunkIndex * Chunk.SIZE;

                                // World-space vertex corner
                                Vector3 worldCorner = v.Position + blockWorldPos;

                                // Round down to voxel grid
                                Vector3i cornerPos = new Vector3i(
                                    (int)MathF.Floor(worldCorner.X),
                                    (int)MathF.Floor(worldCorner.Y),
                                    (int)MathF.Floor(worldCorner.Z)
                                );

                                float brightness = 1.0f;
                                if (blockWorldPos.Y < world.seaLevel)
                                    brightness += (world.seaLevel + blockWorldPos.Y) * 0.05f;

                                result.AddVertex(new VBO.Vertex(worldCorner, uvCoord, v.Normal, brightness));

                                // Add indices (two triangles: 0-1-2 and 2-3-0)
                                result.AddIndices(new List<uint>() {
                                    vertexOffset + 0,
                                    vertexOffset + 1,
                                    vertexOffset + 2,
                                    vertexOffset + 2,
                                    vertexOffset + 3,
                                    vertexOffset + 0
                                });

                                vertexOffset += 4;
                            }
                        }
                    }
                }
            }
            UpdateMeshList(chunkIndex, result);
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
    }
}