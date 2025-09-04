using Minecraft_Clone.World.Chunks;
using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Minecraft_Clone.World
{
    public class WorldGenerator
    {
        public int seaLevel = 64;
        public int maxHeight = 128;
        public float noiseScale = 0.01f;
        public NoiseKit noise = new NoiseKit();


        public BlockType GetBlockAtWorldPos(Vector3i blockWorldPos)
        {
            int worldX = blockWorldPos.X;
            int worldY = blockWorldPos.Y;
            int worldZ = blockWorldPos.Z;

            BlockType type = BlockType.AIR; // default situation

            // three layers of 2d noise
            // layer 1: continental (sea vs land) (2 octavees)
            // layer 2: regional (plains vs mountains) (3 octaves)
            // layer 3: local (mountain details, plain hills) (4 octaves)

            // one layer of 3d noise for volumetric mountains

            float layer1 = noise.Noise((float)worldX * noiseScale/4, (float)worldZ * noiseScale/4);
            // scale from [0, 1] -> [lowestWorldY, maxHeight] for continential
            layer1 = layer1 * (maxHeight);

            //float layer2 = noise

            if (worldY < seaLevel) type = BlockType.WATER;
            if (worldY < layer1) type = BlockType.STONE;
 


            return type;
        }
    }
}
