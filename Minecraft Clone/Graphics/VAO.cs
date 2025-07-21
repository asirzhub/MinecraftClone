// Vertex Array Object

using OpenTK.Graphics.OpenGL4;

namespace Minecraft_Clone.Graphics
{
    public class VAO
    {
        public int ID;

        public void Bind() => GL.BindVertexArray(ID);
        public void UnBind() => GL.BindVertexArray(0);
        public void Delete() => GL.DeleteVertexArray(ID);

        public VAO()
        {
            ID = GL.GenVertexArray();
            Bind();
        }

        public void LinkToVAO(int location, int size, VBO vbo, int stride, int offset)
        {
            Bind();
            vbo.Bind();
            GL.EnableVertexAttribArray(location);
            GL.VertexAttribPointer(location, size, VertexAttribPointerType.Float, false, stride, offset);
            UnBind();
        }
    }
}
