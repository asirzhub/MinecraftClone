// Index Buffer Object

using OpenTK.Graphics.OpenGL4;

namespace Minecraft_Clone.Graphics
{
    internal class IBO
    {
        public int ID;
        public int length;

        public void Bind() => GL.BindBuffer(BufferTarget.ElementArrayBuffer, ID);
        public void UnBind() => GL.BindBuffer(BufferTarget.ElementArrayBuffer, 0);
        public void Delete() => GL.DeleteBuffer(ID);

        public IBO(List<uint> data)
        {
            ID = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, ID);
            GL.BufferData(BufferTarget.ElementArrayBuffer, data.Count * sizeof(uint), data.ToArray(), BufferUsageHint.StaticDraw);
            length = data.Count * sizeof (uint);

        }
    }
}
