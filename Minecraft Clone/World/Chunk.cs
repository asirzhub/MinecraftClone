using OpenTK.Mathematics;

namespace Minecraft_Clone.World
{
    public class Chunk
    {
        public int chunkXIndex;
        public int chunkYIndex;
        public int chunkZIndex;

        public Vector3i ChunkPosition() => new Vector3i(chunkXIndex, chunkYIndex, chunkZIndex); 

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

        public void FillWithBlock(BlockType blockType)
        {
            for (int x = 0; x < CHUNKSIZE; x++)
            {
                for (int y = 0; y < CHUNKSIZE; y++)
                {
                    for (int z = 0; z < CHUNKSIZE;  z++)
                    {
                        SetBlock(x, y, z, blockType);
                    }
                }
            }
            Console.WriteLine("filled " + blockType.ToString() +" into chunk:" + chunkXIndex + " " + chunkYIndex + " " + chunkZIndex);
        }
    }
}
