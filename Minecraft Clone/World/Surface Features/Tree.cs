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
        NoiseKit noise;
        Random RNG;

        public Tree(Vector3i rootCoord, int seed = -1)
        {
            if (seed == -1)
            {
                RNG = new Random();
                seed = RNG.Next(int.MaxValue);
            }
            rootCoordinate = rootCoord;
            this.featureType = SurfaceFeatureType.TREE;
            this.seed = seed;
            noise = new NoiseKit(this.seed);
        }

        public void GrowTree(Vector3i scale)
        {
            this.scale = scale;
            blocks = new byte[scale.X * scale.Y * scale.Z];
            Vector2i centerOffset = (scale.X / 2, scale.Z / 2);

            for (int x = 0; x <= scale.X; x++)
            {
                for (int z = 0; z <= scale.Z; z++)
                {
                    for (int y = 0; y <= scale.Y; y++)
                    {
                        // central log
                        if(x == centerOffset.X && z == centerOffset.Y)
                        {
                            blocks[(y * scale.Y + z) * scale.Z + x] = (byte)BlockType.LOG;
                            continue;
                        }
                        else if (y > 2 && RNG.Next(y) < y)  
                        {
                            blocks[(y * scale.Y + z) * scale.Z + x] = (byte)BlockType.LEAVES;
                            continue;
                        }
                    }
                }
            }
        }
    }
}
