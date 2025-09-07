using OpenTK.Mathematics;

namespace Minecraft_Clone.World.SurfaceFeatures
{
    public enum SurfaceFeatureType {
        TREE,
        TALLGRASS,
        FLOWER
    };

    public interface ISurfaceFeature
    {
        Vector3i rootCoordinate { get; set; } // root coordinate for the surface feature
        SurfaceFeatureType featureType { get; set; } // what kind of feature is it
        Vector3i scale { get; set; } // scale of the feature determines its blocks array
        byte[] blocks { get; set; } // blocks that make up this feature

    }
}
