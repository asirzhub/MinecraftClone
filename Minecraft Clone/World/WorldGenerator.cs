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
        public int seaLevel = 50;
        public float noiseScale = 0.01f;
        public NoiseKit noise = new NoiseKit();

        public BlockType GetBlockAtWorldPos(Vector3i blockWorldPos)
        {
            int worldX = blockWorldPos.X;
            int worldY = blockWorldPos.Y;
            int worldZ = blockWorldPos.Z;

            BlockType type = BlockType.AIR;

            if (worldY < seaLevel)
            {
                type = BlockType.AIR;
            }

            var landBias = 3 * Chunk.SIZE * (noise.Noise((float)worldX * noiseScale, (float)worldZ * noiseScale) - 0.5);

            if (worldY <= seaLevel)
                type = BlockType.WATER;

            if (worldY < landBias && worldY > -64)
                type = BlockType.STONE;

            if (worldY < -50)
                type = BlockType.AIR;

            return type;
        }
    }
}
