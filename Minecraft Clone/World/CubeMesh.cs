using OpenTK.Mathematics;
using static Minecraft_Clone.Graphics.VBO;

namespace Minecraft_Clone.Graphics
{
    // Literally just information on how to make a cube
    public class CubeMesh : MeshData
    {
        public enum Face
        {
            FRONT,
            BACK,
            LEFT,
            RIGHT,
            TOP,
            BOTTOM
        }

        public static readonly int[] indices = {
             0,  1,  3,  1,  2,  3,

             4,  5,  7,  5,  6,  7,

             8,  9, 11,  9, 10, 11,

            12, 13, 15, 13, 14, 15,

            16, 17, 19, 17, 18, 19,

            20, 21, 23, 21, 22, 23
        };

        // PackedVertex arrays for each face (x, y, z, u, v, normalIdx, brightness)
        private static readonly PackedVertex[] PackedFaceFront = new PackedVertex[] {
            new PackedVertex(1, 1, 1, 1f, 1f, 0, 255),
            new PackedVertex(1, 0, 1, 1f, 0f, 0, 255),
            new PackedVertex(0, 0, 1, 0f, 0f, 0, 255),
            new PackedVertex(0, 1, 1, 0f, 1f, 0, 255),
        };

        private static readonly PackedVertex[] PackedFaceBack = new PackedVertex[] {
            new PackedVertex(0, 1, 0, 1f, 1f, 1, 255),
            new PackedVertex(0, 0, 0, 1f, 0f, 1, 255),
            new PackedVertex(1, 0, 0, 0f, 0f, 1, 255),
            new PackedVertex(1, 1, 0, 0f, 1f, 1, 255),
        };

        private static readonly PackedVertex[] PackedFaceLeft = new PackedVertex[] {
            new PackedVertex(0, 1, 1, 1f, 1f, 2, 255),
            new PackedVertex(0, 0, 1, 1f, 0f, 2, 255),
            new PackedVertex(0, 0, 0, 0f, 0f, 2, 255),
            new PackedVertex(0, 1, 0, 0f, 1f, 2, 255),
        };

        private static readonly PackedVertex[] PackedFaceRight = new PackedVertex[] {
            new PackedVertex(1, 1, 0, 1f, 1f, 3, 255),
            new PackedVertex(1, 0, 0, 1f, 0f, 3, 255),
            new PackedVertex(1, 0, 1, 0f, 0f, 3, 255),
            new PackedVertex(1, 1, 1, 0f, 1f, 3, 255),
        };

        private static readonly PackedVertex[] PackedFaceTop = new PackedVertex[] {
            new PackedVertex(0, 1, 1, 0f, 0f, 4, 255),
            new PackedVertex(1, 1, 1, 1f, 0f, 4, 255),
            new PackedVertex(1, 1, 0, 1f, 1f, 4, 255),
            new PackedVertex(0, 1, 0, 0f, 1f, 4, 255),
        };

        private static readonly PackedVertex[] PackedFaceBottom = new PackedVertex[] {
            new PackedVertex(0, 0, 0, 0f, 0f, 5, 255),
            new PackedVertex(1, 0, 0, 1f, 0f, 5, 255),
            new PackedVertex(1, 0, 1, 1f, 1f, 5, 255),
            new PackedVertex(0, 0, 1, 0f, 1f, 5, 255),
        };

        public static readonly Dictionary<Face, PackedVertex[]> PackedFaceVertices = new()
        {
            { Face.FRONT, PackedFaceFront },
            { Face.BACK, PackedFaceBack },
            { Face.LEFT, PackedFaceLeft },
            { Face.RIGHT, PackedFaceRight },
            { Face.TOP, PackedFaceTop },
            { Face.BOTTOM, PackedFaceBottom }
        };
    }    
}
