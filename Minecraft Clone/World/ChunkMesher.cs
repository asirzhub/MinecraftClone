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
        public static void GenerateMesh(Chunk chunk, ChunkWorld world, out List<Vertex> vertices, out List<uint> indices)

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

                        // Calculate the block's position in the world
                        Vector3 blockPos = new Vector3(
                            x + (float)chunk.chunkXIndex * (float)Chunk.CHUNKSIZE,
                            y + (float)chunk.chunkYIndex * (float)Chunk.CHUNKSIZE,
                            z + (float)chunk.chunkZIndex * (float)Chunk.CHUNKSIZE
                        ); // local chunk coord + chunk index

                        bool[] visibility = new bool[6];

                        int gx = chunk.chunkXIndex * Chunk.CHUNKSIZE + x;
                        int gy = chunk.chunkYIndex * Chunk.CHUNKSIZE + y;
                        int gz = chunk.chunkZIndex * Chunk.CHUNKSIZE + z;

                        BlockType thisType = block.Type;
                        bool isWater = block.isWater;

                        Block blockFront = GetBlockGlobal(world, gx, gy, gz + 1);
                        Block blockBack = GetBlockGlobal(world, gx, gy, gz - 1);
                        Block blockLeft = GetBlockGlobal(world, gx - 1, gy, gz);
                        Block blockRight = GetBlockGlobal(world, gx + 1, gy, gz);
                        Block blockTop = GetBlockGlobal(world, gx, gy + 1, gz);
                        Block blockBottom = GetBlockGlobal(world, gx, gy - 1, gz);

                        if (isWater)
                        {
                            // Only show top face if air is above
                            visibility[4] = blockTop.isAir;
                            // Hide all other water faces
                            visibility[0] = visibility[1] = visibility[2] = visibility[3] = visibility[5] = false;
                        }
                        else
                        {
                            // Solid block — show face if neighbor is air or water
                            visibility[0] = blockFront.isAir || blockFront.isWater;    // FRONT
                            visibility[1] = blockBack.isAir || blockBack.isWater;      // BACK
                            visibility[2] = blockLeft.isAir || blockLeft.isWater;      // LEFT
                            visibility[3] = blockRight.isAir || blockRight.isWater;    // RIGHT
                            visibility[4] = blockTop.isAir || blockTop.isWater;        // TOP
                            visibility[5] = blockBottom.isAir || blockBottom.isWater;  // BOTTOM
                        }


                        foreach (CubeMesh.Face face in Enum.GetValues(typeof(CubeMesh.Face)))
                        {
                            int faceIndex = (int)face;
                            if (!visibility[faceIndex]) continue;
                            //Console.WriteLine($"Adding a face for the block at world coordinate: {blockPos}");

                            var faceVerts = CubeMesh.FaceVertices[face];
                            for (int i = 0; i < 4; i++)
                            {
                                var v = faceVerts[i];
                                var faceUVs = BlockRegistry.Types[block.Type].FaceUVs;
                                Vector2 tile = faceUVs[faceIndex];
                                tile += v.TexCoord; // apply the corners
                                Vector2 uvCoord = tile / 8f; // scale down to uv coordinates
                                uvCoord.Y = 1.0f - uvCoord.Y; // flip y because stbimagesharp trolls you hard
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

            Console.WriteLine($"In chunk with index {chunk.ChunkPosition()} is Verts: {vertices.Count}, Indices: {indices.Count}");
        }

        static Block GetBlockGlobal(ChunkWorld world, int globalX, int globalY, int globalZ)
        {
            Vector3i chunkIndex = new Vector3i(
                globalX / Chunk.CHUNKSIZE,
                globalY / Chunk.CHUNKSIZE,
                globalZ / Chunk.CHUNKSIZE
            );

            if (globalX < 0 && globalX % Chunk.CHUNKSIZE != 0) chunkIndex.X--;
            if (globalY < 0 && globalY % Chunk.CHUNKSIZE != 0) chunkIndex.Y--;
            if (globalZ < 0 && globalZ % Chunk.CHUNKSIZE != 0) chunkIndex.Z--;

            if (!world.chunks.TryGetValue(chunkIndex, out var neighborChunk)) return new Block(BlockType.AIR);

            int lx = (globalX % Chunk.CHUNKSIZE + Chunk.CHUNKSIZE) % Chunk.CHUNKSIZE;
            int ly = (globalY % Chunk.CHUNKSIZE + Chunk.CHUNKSIZE) % Chunk.CHUNKSIZE;
            int lz = (globalZ % Chunk.CHUNKSIZE + Chunk.CHUNKSIZE) % Chunk.CHUNKSIZE;

            return neighborChunk.GetBlock(lx, ly, lz);
        }

    }
}
