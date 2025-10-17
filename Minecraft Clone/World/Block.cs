namespace Minecraft_Clone.World
{
    /// A block only knows what it is, and nothing else.
    /// </summary>
    public struct Block(BlockType type)
    {
        public BlockType Type = type;

        public bool IsWater => Type == BlockType.WATER;
        public bool IsSolid => BlockRegistry.Types[Type].IsSolid;
    }
}
