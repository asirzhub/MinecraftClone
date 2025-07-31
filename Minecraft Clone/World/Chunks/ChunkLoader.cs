using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenTK.Mathematics;

namespace Minecraft_Clone.World.Chunks
{
    public class ChunkLoader
    {
        //private Vector3 cameraPosition;
        private int loadRadius = 8;
        Dictionary<Vector3i, bool> chunkLoadList = new Dictionary<Vector3i, bool>();

        public ChunkLoader()
        {
        }

        public Dictionary<Vector3i, bool> GetChunksToLoad(Vector3i currentChunkIndex, Camera camera, int radius)
        {
            // from the current chunk, and knowing which way the camera faces, pick out which chunks to load
            // obviously, load the chunk that the camera is in, then branch out in each direction to radius
            // all this function does is mark the indexes of the chunks that should be rendered

            // algorithm idea:
            // check if the chunk is even in the direction we are facing (dot product)
            // if its less than a threshold, hide it

            int visible = 0;
            Vector3 viewDir = camera.front.Normalized();

            for (int x = currentChunkIndex.X - radius;  x <= currentChunkIndex.X + radius; x++)
            {
                for(int y = currentChunkIndex .Y - radius; y <= currentChunkIndex.Y + radius; y++)
                {
                    for(int z = currentChunkIndex.Z - radius; z <= currentChunkIndex.Z + radius; z++)
                    {
                        if (x == currentChunkIndex.X && y == currentChunkIndex.Y && z == currentChunkIndex.Z)
                        {
                            SetChunkVisibility(currentChunkIndex, true);
                            visible++;
                            continue;
                        }

                        Vector3i targetChunkIndex = (x, y, z);

                        SetChunkVisibility(targetChunkIndex, true);

                        visible++;

                    }
                }
            }
            Console.WriteLine($"Total Chunks {chunkLoadList.Count}: visibile is {visible} ({(float)visible/(float)chunkLoadList.Count}%)");
            return chunkLoadList;
        }

        public void SetChunkVisibility(Vector3i index, bool visible)
        {
            if (chunkLoadList.ContainsKey(index))
            {
                chunkLoadList[index] = visible;
            }
            else
            {
                chunkLoadList.Add(index, visible);
            }
        }

    }
}
