namespace Minecraft_Clone.World
{
    public struct Block
    {
        public BlockType Type; 

        // <summary>
        /// A block only knows what it is, and nothing else.
        /// </summary>
        public Block(BlockType type)
        {
            this.Type = type;
        }

        public bool isAir => Type == BlockType.AIR; 
        public bool isWater => Type == BlockType.WATER;

        public bool isSolid => !(isAir || isWater);
    }
}
