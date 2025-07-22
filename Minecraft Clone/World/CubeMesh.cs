using OpenTK.Mathematics;
using static Minecraft_Clone.Graphics.VBO;

namespace Minecraft_Clone.Graphics
{
    // Literally just information on how to make a cube
    public static class CubeMesh
    {

        public static readonly int[] indices = {
        0, 1, 3,
        1, 2, 3,

        4, 5, 7,
        5, 6, 7,

        8, 9, 11,
        9, 10, 11,

        12, 13, 15,
        13, 14, 15,

        16, 17, 19,
        17, 18, 19,

        20, 21, 23,
        21, 22, 23
        };

        private static readonly Vertex[] FaceFront = new Vertex[]
        {
            new Vertex(new Vector3(1, 1, 1), new Vector2(1, 1), Vector3.UnitZ),
            new Vertex(new Vector3(1, 0, 1), new Vector2(1, 0), Vector3.UnitZ),
            new Vertex(new Vector3(0, 0, 1), new Vector2(0, 0), Vector3.UnitZ),
            new Vertex(new Vector3(0, 1, 1), new Vector2(0, 1), Vector3.UnitZ),
        };

        private static readonly Vertex[] FaceBack = new Vertex[]
        {
            new Vertex(new Vector3(0, 1, 0), new Vector2(1, 1), -Vector3.UnitZ),
            new Vertex(new Vector3(0, 0, 0), new Vector2(1, 0), -Vector3.UnitZ),
            new Vertex(new Vector3(1, 0, 0), new Vector2(0, 0), -Vector3.UnitZ),
            new Vertex(new Vector3(1, 1, 0), new Vector2(0, 1), -Vector3.UnitZ),
        };

        private static readonly Vertex[] FaceLeft = new Vertex[]
        {
            new Vertex(new Vector3(0, 1, 1), new Vector2(1, 1), -Vector3.UnitX),
            new Vertex(new Vector3(0, 0, 1), new Vector2(1, 0), -Vector3.UnitX),
            new Vertex(new Vector3(0, 0, 0), new Vector2(0, 0), -Vector3.UnitX),
            new Vertex(new Vector3(0, 1, 0), new Vector2(0, 1), -Vector3.UnitX),
        };

        private static readonly Vertex[] FaceRight = new Vertex[]
        {
            new Vertex(new Vector3(1, 1, 0), new Vector2(1, 1), Vector3.UnitX),
            new Vertex(new Vector3(1, 0, 0), new Vector2(1, 0), Vector3.UnitX),
            new Vertex(new Vector3(1, 0, 1), new Vector2(0, 0), Vector3.UnitX),
            new Vertex(new Vector3(1, 1, 1), new Vector2(0, 1), Vector3.UnitX),
        };

        private static readonly Vertex[] FaceTop = new Vertex[]
        {
            new Vertex(new Vector3(0, 1, 1), new Vector2(0, 0), Vector3.UnitY),
            new Vertex(new Vector3(1, 1, 1), new Vector2(1, 0), Vector3.UnitY),
            new Vertex(new Vector3(1, 1, 0), new Vector2(1, 1), Vector3.UnitY),
            new Vertex(new Vector3(0, 1, 0), new Vector2(0, 1), Vector3.UnitY),
        };

        private static readonly Vertex[] FaceBottom = new Vertex[]
        {
            new Vertex(new Vector3(0, 0, 0), new Vector2(0, 0), -Vector3.UnitY),
            new Vertex(new Vector3(1, 0, 0), new Vector2(1, 0), -Vector3.UnitY),
            new Vertex(new Vector3(1, 0, 1), new Vector2(1, 1), -Vector3.UnitY),
            new Vertex(new Vector3(0, 0, 1), new Vector2(0, 1), -Vector3.UnitY),
        };

        public enum Face
        {
            FRONT,
            BACK,
            LEFT,
            RIGHT,
            TOP,
            BOTTOM
        }

        public static readonly Dictionary<Face, Vertex[]> FaceVertices = new()
        {
            { Face.FRONT, FaceFront },
            { Face.BACK, FaceBack },
            { Face.LEFT, FaceLeft },
            { Face.RIGHT, FaceRight },
            { Face.TOP, FaceTop },
            { Face.BOTTOM, FaceBottom }
        };

    }
}
