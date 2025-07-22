using OpenTK.Mathematics;
using System.Runtime.InteropServices;
using static Minecraft_Clone.Graphics.VBO;

namespace Minecraft_Clone.Graphics
{
    public class VertexUtils
    {

        // <summary>
        /// Squash a human-readable vertex into a list of floats to be used as VBO
        /// </summary>
        public static List<float> FlattenVertices(List<Vertex> vertices)
        {
            List<float> data = new List<float>();
            int stride = Marshal.SizeOf(typeof(Vertex));

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
                data.Add(v.brightness);
            }

            return data;
        }
    }
}
