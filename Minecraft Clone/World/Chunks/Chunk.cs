using OpenTK.Mathematics;

namespace Minecraft_Clone.World.Chunks
{
    public class Chunk
    {
        public const int SIZE = 16; // same size in all coordinates

        public Block[] blocks = new Block[SIZE * SIZE * SIZE];

        // <summary>
        /// Create a new chunk at these chunk coordinates
        /// </summary>
        public Chunk()
        {
        }

        public Block GetBlock(int x, int y, int z) 
            => blocks[(y * SIZE + z) * SIZE + x];

        public void SetBlock(int x, int y, int z, BlockType type) 
            => blocks[(y * SIZE + z) * SIZE + x] = new Block(type);

        // <summary>
        /// Fill a chunk with some block.
        /// </summary>
        public void FillWithBlock(BlockType blockType)
        {
            for (int x = 0; x < SIZE; x++)
            {
                for (int y = 0; y < SIZE; y++)
                {
                    for (int z = 0; z < SIZE;  z++)
                    {
                        SetBlock(x, y, z, blockType);
                    }
                }
            }
            Console.WriteLine("filled " + blockType.ToString() +" into chunk");
        }
    }
}
