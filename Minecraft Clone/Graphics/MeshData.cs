using System.Runtime.InteropServices;
using static Minecraft_Clone.Graphics.VBO;
using static OpenTK.Graphics.OpenGL.GL;

namespace Minecraft_Clone.Graphics
{
    public class MeshData
    {
        public List<VBO.Vertex> Vertices = new();
        public List<uint> Indices = new();

        public VAO vao;
        public VBO vbo;
        public IBO ibo;

        public MeshData()
        {
        }

        public void Upload()
        {
            if (vao == null) { vao = new VAO(); }
            if (vbo == null) { vbo = new VBO(VertexUtils.FlattenVertices(Vertices).ToArray()); }
            if (ibo == null) { ibo = new IBO(Indices); }

            int stride = Marshal.SizeOf<Vertex>();
            int floatSize = sizeof(float);

            vao.Bind();
            vbo.Bind();
            vao.LinkToVAO(0, 3, vbo, stride, 0);
            vao.LinkToVAO(1, 2, vbo, stride, 3 * floatSize);
            vao.LinkToVAO(2, 3, vbo, stride, 5 * floatSize);
            vao.LinkToVAO(3, 1, vbo, stride, 8 * floatSize);
            ibo.Bind();
        }

        public void AddVertex(VBO.Vertex v)
        {
            Vertices.Add(v);
        }
        public void AddVertices(List<VBO.Vertex> vertices) 
        {
            Vertices.AddRange(vertices);
        }
        public void AddIndices(List<uint> indices)
        {
            Indices.AddRange(indices);
        }
        public void Dispose()
        {
            vao?.UnBind();
            vao?.Dispose();
            vbo?.UnBind();
            vbo?.Dispose();
            ibo?.UnBind();
            ibo?.Dispose();

            Vertices.Clear();
            Indices.Clear();
        }
    }
}
