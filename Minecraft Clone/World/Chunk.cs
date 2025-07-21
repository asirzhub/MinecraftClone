    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    namespace Minecraft_Clone.World
    {
        public class Chunk
        {
            public int chunkXIndex;
            public int chunkYIndex;
            public int chunkZIndex;

            public const int CHUNKSIZE = 16; // same size in all coordinates

            public Block[] blocks = new Block[CHUNKSIZE * CHUNKSIZE * CHUNKSIZE];

            public Chunk(int xIndex, int yIndex, int zIndex)
            {
                chunkXIndex = xIndex;
                chunkYIndex = yIndex;
                chunkZIndex = zIndex;
            }

            public Block GetBlock(int x, int y, int z) => blocks[(y * CHUNKSIZE + z) * CHUNKSIZE + x];

            public void SetBlock(int x, int y, int z, BlockType type) => blocks[(y * CHUNKSIZE + z) * CHUNKSIZE + x] = new Block(type);

        }
    }
