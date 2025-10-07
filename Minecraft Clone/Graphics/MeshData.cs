using System.Runtime.InteropServices;
using static Minecraft_Clone.Graphics.VBO;
using static OpenTK.Graphics.OpenGL.GL;

namespace Minecraft_Clone.Graphics
{
    public class MeshData
    {
        public List<PackedVertex> Vertices = new();
        public List<uint> Indices = new();

        public VAO vao;
        public VBO vbo;
        public IBO ibo;

        public int stride = -1;

        public MeshData()
        {
        }

        public void Upload()
        {
            if (vao == null) { vao = new VAO(); }
            if (vbo == null) { vbo = new VBO(FlattenPackedVertices(Vertices)); }
            if (ibo == null) { ibo = new IBO(Indices); }

            if(stride == -1) stride = Marshal.SizeOf(typeof(PackedVertex));

            vao.Bind();
            vbo.Bind();
            vao.LinkToVAOInt(0, 1, vbo, stride, 0);
            vao.LinkToVAO(1, 2, vbo, stride, 4);
            ibo.Bind();
        }

        public void AddVertex(VBO.PackedVertex v)
        {
            Vertices.Add(v);
        }
        public void AddVertices(List<PackedVertex> vertices) 
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
