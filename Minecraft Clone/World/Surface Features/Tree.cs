using OpenTK.Mathematics;

namespace Minecraft_Clone.World.SurfaceFeatures
{
    public class Tree : ISurfaceFeature
    {
        public Vector3i rootCoordinate { get; set; }
        public SurfaceFeatureType featureType { get; set; }
        public Vector3i scale { get; set; }
        public byte[] blocks { get; set; }

        int seed;
        public Random RNG;

        public Tree(int seed)
        {
            RNG = new Random(seed);
            this.seed = seed;
            featureType = SurfaceFeatureType.TREE;
        }

        public void GenerateTreeBlocks(Vector3i scale)
        {
            this.scale = scale;
            blocks = new byte[scale.X * scale.Y * scale.Z];
            Vector2i centerOffset = (scale.X / 2, scale.Z / 2);

            for (int x = 0; x < scale.X; x++)
            {
                for (int z = 0; z < scale.Z; z++)
                {
                    for (int y = 0; y < scale.Y; y++)
                    {
                        BlockType result = BlockType.AIR;
                        // central log
                        if(x == centerOffset.X && z == centerOffset.Y)
                        {
                            result = BlockType.LOG;
                        }
                        else if (y > 2 && RNG.Next(y) < y)  
                        {
                            result = BlockType.LEAVES;
                        }
                        blocks[(y * scale.Z + z) * scale.X + x] = (byte)result;
                    }
                }
            }
        }
    }
}
