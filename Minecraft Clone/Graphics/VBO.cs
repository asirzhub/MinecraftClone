// Vertex Buffer Object

using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;

namespace Minecraft_Clone.Graphics
{
    public class VBO
    {
        public int ID;

        public void Bind() => GL.BindBuffer(BufferTarget.ArrayBuffer, ID);
        public void UnBind() => GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
        public void Delete() => GL.DeleteBuffer(ID);


        // human-readable vertex info
        public struct Vertex
        {
            public Vector3 Position;
            public Vector2 TexCoord;
            public Vector3 Normal;
            public float brightness; // 👈 must be float!

            public Vertex(Vector3 position, Vector2 texCoord, Vector3 normal, float brightness = 1.0f)
            {
                this.Position = position;
                this.TexCoord = texCoord;
                this.Normal = normal;
                this.brightness = brightness;
            }
        }


        // <summary>
        /// VBO built with floats
        /// </summary>
        public VBO(float[] data)
        {
            ID = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ArrayBuffer, ID);
            GL.BufferData(BufferTarget.ArrayBuffer, data.Length * sizeof(float), data, BufferUsageHint.StaticDraw);
        }
    }
}
