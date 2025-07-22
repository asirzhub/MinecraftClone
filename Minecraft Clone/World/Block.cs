namespace Minecraft_Clone.World
{
    public struct Block
    {
        public BlockType Type; // we DONT store a blocktypedata here, bc there will be millions of these
        // its smarter to store ONLY the enum. and then use a registry to learn about a block properties

        public Block(BlockType type)
        {
            this.Type = type;
        }

        public bool isAir => Type == BlockType.AIR; 
        public bool isWater => Type == BlockType.WATER;
    }
}
