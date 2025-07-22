using OpenTK.Mathematics;
using static Minecraft_Clone.Graphics.VBO;

namespace Minecraft_Clone.Graphics
{
    public class VertexUtils
    {
        // to squash human-readable vertex into a float array to send to gpu
        public static List<float> FlattenVertices(List<Vertex> vertices, int stride = 8)
        {
            List<float> data = new List<float>();

            for (int i = 0; i < vertices.Count; i++)
            {
                var v = vertices[i];
                int baseIndex = i * stride;

                data.Add(v.Position.X);
                data.Add(v.Position.Y);
                data.Add(v.Position.Z);
                data.Add(v.TexCoord.X);
                data.Add(v.TexCoord.Y);
                data.Add(v.Normal.X);
                data.Add(v.Normal.Y);
                data.Add(v.Normal.Z);
            }

            return data;
        }
    }
}
