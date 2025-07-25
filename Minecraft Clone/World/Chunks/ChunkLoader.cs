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

            Dictionary<Vector3i, bool> chunkLoadList = new Dictionary<Vector3i, bool>();
            int visible = 0;
            Vector3 viewDir = camera.front.Normalized();

            for (int x = -radius;  x <= radius; x++)
            {
                for(int y = - radius; y <= radius; y++)
                {
                    for(int z = - radius; z <= radius; z++)
                    {
                        // skip the xyz if it is behind the plane (cuts loop time in half... hopefully?)
                        // for every chunk that is touching the cone, mark that index as true
                        Vector3 chunkDir = ((Vector3)(x, y, z) - (Vector3)currentChunkIndex).Normalized();
                        float dot = Vector3.Dot((Vector3)viewDir, (Vector3)chunkDir);
                        if (dot < 0)
                            chunkLoadList.Add((x, y, z), false);
                        else
                        {
                            Vector3i offset = new Vector3i(x, y, z);
                            Vector3i chunkIndex = currentChunkIndex + offset;
                            chunkLoadList.Add(chunkIndex, true);

                            visible++;
                        }
                    }
                }
            }
            Console.WriteLine($"Total Chunks {chunkLoadList.Count}: visibile is {visible} ({visible/chunkLoadList.Count * 100}%)");
            return chunkLoadList;
        }
    }
}
