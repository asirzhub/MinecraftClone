using System.Collections.Generic;
using Minecraft_Clone.Graphics;
using OpenTK.Mathematics;
using static Minecraft_Clone.Graphics.VBO;
using Minecraft_Clone.World;

namespace Minecraft_Clone.Rendering
{
    public static class ChunkMesher
    {

        // <summary>
        /// Turn the chunk data into a VBO and IBO. This will remove interior faces of solid blocks, and keep only the surface of water.
        /// </summary>
        public static void GenerateMesh(Chunk chunk, ChunkWorld world, out List<Vertex> vertices, out List<uint> indices,
            out List<Vertex> waterVertices, out List<uint> waterIndices)

        {
            vertices = new List<Vertex>();
            indices = new List<uint>();
            waterVertices = new List<Vertex>();
            waterIndices = new List<uint>();

            uint vertexOffset = 0;
            uint waterVertexOffset = 0;

            // for every block in this chunk...
            for (int x = 0; x < Chunk.CHUNKSIZE; x++)
            {
                for (int y = 0; y < Chunk.CHUNKSIZE; y++)
                {
                    for (int z = 0; z < Chunk.CHUNKSIZE; z++)
                    {
                        Block block = chunk.GetBlock(x, y, z);
                        if (block.isAir) continue; // skip it if it's air

                        // Calculate the block's position in the world (local chunk coord + chunk index

                        bool[] visibility = new bool[6]; // track face visibilities.

                        int worldBlockPosX = chunk.chunkXIndex * Chunk.CHUNKSIZE + x;
                        int worldBlockPosY = chunk.chunkYIndex * Chunk.CHUNKSIZE + y;
                        int worldBlockPosZ = chunk.chunkZIndex * Chunk.CHUNKSIZE + z;

                        BlockType thisType = block.Type;
                        bool isWater = block.isWater;

                        Block blockFront = GetBlockGlobal(world, worldBlockPosX, worldBlockPosY, worldBlockPosZ + 1);
                        Block blockBack = GetBlockGlobal(world, worldBlockPosX, worldBlockPosY, worldBlockPosZ - 1);
                        Block blockLeft = GetBlockGlobal(world, worldBlockPosX - 1, worldBlockPosY, worldBlockPosZ);
                        Block blockRight = GetBlockGlobal(world, worldBlockPosX + 1, worldBlockPosY, worldBlockPosZ);
                        Block blockTop = GetBlockGlobal(world, worldBlockPosX, worldBlockPosY + 1, worldBlockPosZ);
                        Block blockBottom = GetBlockGlobal(world, worldBlockPosX, worldBlockPosY - 1, worldBlockPosZ);

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

                        // for each face of the block
                        foreach (CubeMesh.Face face in Enum.GetValues(typeof(CubeMesh.Face)))
                        {
                            int faceIndex = (int)face;
                            if (!visibility[faceIndex]) continue; // skip the vertices if its invisible

                            var faceVerts = CubeMesh.FaceVertices[face];

                            for (int i = 0; i < 4; i++) // four corners
                            {
                                var v = faceVerts[i];
                                var faceUVs = BlockRegistry.Types[block.Type].FaceUVs;
                                Vector2 tile = faceUVs[faceIndex];
                                tile += v.TexCoord; // apply the corner offset
                                Vector2 uvCoord = tile / 8f; // scale down to uv coordinates
                                uvCoord.Y = 1.0f - uvCoord.Y; // flip y because stbimagesharp trolls you hard

                                Vector3 blockPos = new Vector3(worldBlockPosX, worldBlockPosY, worldBlockPosZ);

                                // World-space vertex corner
                                Vector3 worldCorner = v.Position + blockPos;

                                // Round down to voxel grid
                                Vector3i cornerPos = new Vector3i(
                                    (int)MathF.Floor(worldCorner.X),
                                    (int)MathF.Floor(worldCorner.Y),
                                    (int)MathF.Floor(worldCorner.Z)
                                );

                                // Find neighbor offsets — approximate by projecting normal and tangent axes
                                Vector3 normal = v.Normal;
                                Vector3 tangentA = Vector3.Cross(normal, Vector3.UnitY);
                                if (tangentA == Vector3.Zero) tangentA = Vector3.UnitX; // fallback
                                Vector3 tangentB = Vector3.Cross(normal, tangentA);

                                // Count neighbors (ambient occlusion)
                                int occlusion = CountOccludingNeighbors(world, cornerPos, tangentA, tangentB);

                                // Final brightness: range 100 → 25
                                float brightness = 1.0f - 0.15f * occlusion; 

                                if (thisType == BlockType.WATER)
                                    waterVertices.Add(new Vertex(v.Position + blockPos, uvCoord, v.Normal, brightness));
                                else
                                    vertices.Add(new Vertex(v.Position + blockPos, uvCoord, v.Normal, brightness));
                            }

                            // add info to the correect indices too
                            if (thisType == BlockType.WATER) { 
                                // Add indices (two triangles: 0-1-2 and 2-3-0)
                                waterIndices.Add(waterVertexOffset + 0);
                                waterIndices.Add(waterVertexOffset + 1);
                                waterIndices.Add(waterVertexOffset + 2);

                                waterIndices.Add(waterVertexOffset + 2);
                                waterIndices.Add(waterVertexOffset + 3);
                                waterIndices.Add(waterVertexOffset + 0);
                                waterVertexOffset += 4;
                            }
                            else
                            {
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
            }

            //Console.WriteLine($"In chunk with index {chunk.ChunkPosition()} is Verts: {vertices.Count}, Indices: {indices.Count}");
        }

        // <summary>
        /// Get a block's global position from the whole world.
        /// </summary>
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

        static int CountOccludingNeighbors(ChunkWorld world, Vector3i pos, Vector3 offsetA, Vector3 offsetB)
        {
            int count = 0;

            Vector3i side = pos + new Vector3i((int)offsetA.X, (int)offsetA.Y, (int)offsetA.Z);
            Vector3i up = pos + new Vector3i((int)offsetB.X, (int)offsetB.Y, (int)offsetB.Z);
            Vector3i corner = pos + new Vector3i((int)(offsetA.X + offsetB.X), (int)(offsetA.Y + offsetB.Y), (int)(offsetA.Z + offsetB.Z));

            if (!GetBlockGlobal(world, side.X, side.Y, side.Z).isAir) count++;
            if (!GetBlockGlobal(world, up.X, up.Y, up.Z).isAir) count++;
            if (!GetBlockGlobal(world, corner.X, corner.Y, corner.Z).isAir) count++;

            return count;
        }

    }
}
