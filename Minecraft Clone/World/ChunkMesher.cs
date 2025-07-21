using System.Collections.Generic;
using Minecraft_Clone.Graphics;
using OpenTK.Mathematics;
using static Minecraft_Clone.Graphics.VBO;
using Minecraft_Clone.World;

namespace Minecraft_Clone.Rendering
{
    public static class ChunkMesher
    {
        // input: a chunk
        // output: list of vertices that only form the outer shell of said chunk
        public static void GenerateMesh(Chunk chunk, out List<Vertex> vertices, out List<uint> indices)
        {
            vertices = new List<Vertex>();
            indices = new List<uint>();
            uint vertexOffset = 0;

            for (int x = 0; x < Chunk.CHUNKSIZE; x++)
            {
                for (int y = 0; y < Chunk.CHUNKSIZE; y++)
                {
                    for (int z = 0; z < Chunk.CHUNKSIZE; z++)
                    {
                        Block block = chunk.GetBlock(x, y, z);
                        if (block.isAir) continue;

                        Vector3 blockPos = new(x, y, z);

                        bool[] visibility = new bool[6];
                        visibility[0] = (z == Chunk.CHUNKSIZE - 1) || chunk.GetBlock(x, y, z + 1).isAir; // FRONT
                        visibility[1] = (z == 0) || chunk.GetBlock(x, y, z - 1).isAir;                   // BACK
                        visibility[2] = (x == 0) || chunk.GetBlock(x - 1, y, z).isAir;                   // LEFT
                        visibility[3] = (x == Chunk.CHUNKSIZE - 1) || chunk.GetBlock(x + 1, y, z).isAir; // RIGHT
                        visibility[4] = (y == Chunk.CHUNKSIZE - 1) || chunk.GetBlock(x, y + 1, z).isAir; // TOP
                        visibility[5] = (y == 0) || chunk.GetBlock(x, y - 1, z).isAir;                   // BOTTOM

                        foreach (CubeMesh.Face face in Enum.GetValues(typeof(CubeMesh.Face)))
                        {
                            int faceIndex = (int)face;
                            if (!visibility[faceIndex]) continue;

                            var faceVerts = CubeMesh.FaceVertices[face];
                            for (int i = 0; i < 4; i++)
                            {
                                var v = faceVerts[i];
                                var faceUVs = BlockRegistry.Types[block.Type].FaceUVs;
                                Vector2 tile = faceUVs[faceIndex];
                                tile += v.TexCoord; // apply the corners
                                Vector2 uvCoord = tile / 8f; // scale down to uv coordinates
                                uvCoord.Y = 1.0f - uvCoord.Y; // flip y...?
                                vertices.Add(new Vertex(v.Position + blockPos, uvCoord, v.Normal));
                            }

                            // Add indices (two triangles: 0-1-2 and 2-3-0)
                            indices.Add(vertexOffset + 0);
                            indices.Add(vertexOffset + 1);
                            indices.Add(vertexOffset + 2);

                            indices.Add(vertexOffset + 2);
                            indices.Add(vertexOffset + 3);
                            indices.Add(vertexOffset + 0);

                            vertexOffset += 4;
                        }
                    }
                }
            }

            Console.WriteLine($"Verts: {vertices.Count}, Indices: {indices.Count}");
        }
    }
}
