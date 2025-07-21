using OpenTK.Mathematics;
using static Minecraft_Clone.Graphics.VBO;

namespace Minecraft_Clone.Graphics
{
    public class VertexUtils
    {
        // to squash human-readable vertex into a float array to send to gpu
        public static float[] FlattenVertices(List<Vertex> vertices, int stride = 8)
        {
            float[] data = new float[vertices.Count * stride];

            for (int i = 0; i < vertices.Count; i++)
            {
                var v = vertices[i];
                int baseIndex = i * stride;

                data[baseIndex + 0] = v.Position.X;
                data[baseIndex + 1] = v.Position.Y;
                data[baseIndex + 2] = v.Position.Z;
                data[baseIndex + 3] = v.TexCoord.X;
                data[baseIndex + 4] = v.TexCoord.Y;
                data[baseIndex + 5] = v.Normal.X;
                data[baseIndex + 6] = v.Normal.Y;
                data[baseIndex + 7] = v.Normal.Z;
            }

            return data;
        }
    }
}
